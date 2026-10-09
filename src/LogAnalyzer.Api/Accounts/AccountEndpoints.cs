using System.Net.Mail;
using System.Security.Claims;
using LogAnalyzer.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LogAnalyzer.Api.Accounts;

/// <summary>The account endpoints under /auth (behind nginx: /api/auth). See docs/accounts-api.md.</summary>
internal static class AccountEndpoints
{
    // Verified when an email address has no account, so that answer also pays for a slow hash. A wrong password for an
    // existing account still answers a few milliseconds later, because it also records the failure. That is an accepted
    // risk; see Risks in openspec/changes/archive/2026-10-10-add-user-accounts/design.md.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString());

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/auth").WithTags("Accounts");

        // Each route declares its answers for the OpenAPI document that the Angular client is generated from.
        // Handlers that return IResult declare nothing themselves, so the document would show a bare 200.
        accounts.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .Produces<AccountResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        accounts.MapPost("/sign-in", SignInAsync)
            .AllowAnonymous()
            .Produces<AccountResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        // Cast to Delegate: as a plain method group it would bind to the RequestDelegate overload, which ignores the returned 204.
        accounts.MapPost("/sign-out", (Delegate)SignOutAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        accounts.MapGet("/me", MeAsync)
            .Produces<AccountResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegisterAsync(CredentialsRequest request, UserManager<AppUser> users)
    {
        // Checked here rather than left to the account store, so each request gets exactly one code, the email address first.
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (!IsValidEmail(email))
        {
            return AccountErrors.InvalidEmail.ToResult();
        }

        // Counted in Unicode code points, as NIST SP 800-63B asks. string.Length would count an emoji such as U+1F600 twice.
        var passwordLength = password.EnumerateRunes().Count();
        if (passwordLength < AccountRules.MinPasswordLength)
        {
            return AccountErrors.PasswordTooShort.ToResult();
        }

        if (passwordLength > AccountRules.MaxPasswordLength)
        {
            return AccountErrors.PasswordTooLong.ToResult();
        }

        var user = new AppUser { UserName = email, Email = email };
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, password);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two registrations for the same address raced past the duplicate check; the unique index caught the second.
            return AccountErrors.EmailTaken.ToResult();
        }

        if (result.Succeeded)
        {
            return TypedResults.Created((string?)null, new AccountResponse(user.Id, email));
        }

        var errorCodes = result.Errors.Select(error => error.Code).ToHashSet(StringComparer.Ordinal);
        if (errorCodes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName))
            || errorCodes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AccountErrors.EmailTaken.ToResult();
        }

        if (errorCodes.Contains(nameof(IdentityErrorDescriber.InvalidEmail))
            || errorCodes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
        {
            return AccountErrors.InvalidEmail.ToResult();
        }

        throw new InvalidOperationException($"Registration failed unexpectedly: {string.Join(", ", errorCodes)}.");
    }

    private static async Task<IResult> SignInAsync(
        CredentialsRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signInManager,
        HttpContext httpContext)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        // An address registration refuses cannot have an account, so it is not looked up. This also keeps characters
        // PostgreSQL cannot compare, such as NUL, out of the query.
        var user = IsValidEmail(email) ? await users.FindByEmailAsync(email) : null;
        if (user is null)
        {
            users.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyPasswordHash, password);
            return AccountErrors.InvalidCredentials.ToResult();
        }

        // Counts the failure toward the lockout when the password is wrong.
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return AccountErrors.AccountLocked.ToResult();
        }

        if (!result.Succeeded)
        {
            return AccountErrors.InvalidCredentials.ToResult();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        await httpContext.SignInAsync(AccountSessions.Scheme, principal, new AuthenticationProperties { IsPersistent = false });
        return TypedResults.Ok(new AccountResponse(user.Id, user.Email!));
    }

    // The cookie handler deletes the stored session and expires the cookie, so a copied cookie stops working too.
    private static async Task<IResult> SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(AccountSessions.Scheme);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null
            ? AccountErrors.SignInRequired.ToResult()
            : TypedResults.Ok(new AccountResponse(user.Id, user.Email!));
    }

    // Control characters are refused before parsing: SMTP allows none in an address, PostgreSQL cannot store a NUL, and
    // the parser accepts one escaped in a quoted local part.
    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= AccountRules.MaxEmailLength
        && !email.Any(char.IsControl)
        && MailAddress.TryCreate(email, out var address)
        && string.Equals(address.Address, email, StringComparison.Ordinal);

    internal sealed record CredentialsRequest(string? Email, string? Password);

    internal sealed record AccountResponse(Guid Id, string Email);
}
