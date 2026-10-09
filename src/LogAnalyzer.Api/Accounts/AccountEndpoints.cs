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
    // Verified when an email address has no account, so the response time does not reveal whether it exists.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString());

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/auth").WithTags("Accounts");

        accounts.MapPost("/register", RegisterAsync).AllowAnonymous();
        accounts.MapPost("/sign-in", SignInAsync).AllowAnonymous();
        // Cast to Delegate: as a plain method group it would bind to the RequestDelegate overload, which ignores the returned 204.
        accounts.MapPost("/sign-out", (Delegate)SignOutAsync);
        accounts.MapGet("/me", MeAsync);

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

        if (password.Length < AccountRules.MinPasswordLength)
        {
            return AccountErrors.PasswordTooShort.ToResult();
        }

        if (password.Length > AccountRules.MaxPasswordLength)
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
        var user = email.Length == 0 ? null : await users.FindByEmailAsync(email);
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

    private static bool IsValidEmail(string email) =>
        email.Length is > 0 and <= AccountRules.MaxEmailLength
        && MailAddress.TryCreate(email, out var address)
        && string.Equals(address.Address, email, StringComparison.Ordinal);

    internal sealed record CredentialsRequest(string? Email, string? Password);

    internal sealed record AccountResponse(Guid Id, string Email);
}
