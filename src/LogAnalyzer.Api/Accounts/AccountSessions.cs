using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace LogAnalyzer.Api.Accounts;

/// <summary>
/// The session cookie: HttpOnly, always Secure, SameSite=Strict, a browser-session cookie (no Expires),
/// backed by a server-side session that ends after <see cref="IdleTimeout"/> without use.
/// The API never redirects to a login page; it answers 401 or 403 with problem details.
/// Every endpoint requires a signed-in user unless it opts out with <c>AllowAnonymous()</c>, so a new endpoint is protected by default.
/// </summary>
internal static class AccountSessions
{
    public const string Scheme = "Session";

    public const string CookieName = "la_session";

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(8);

    // A used session is renewed (expiry moved to now + IdleTimeout) at most this often, to spare a database write per request.
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAccountSessions(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseTicketStore>();

        services.AddAuthentication(Scheme)
            .AddCookie(Scheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
                options.ExpireTimeSpan = IdleTimeout;
                options.SlidingExpiration = true;
                // Restart the idle period on use. The built-in sliding expiration alone renews only after half the lifetime.
                options.Events.OnValidatePrincipal = context =>
                {
                    var now = (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow();
                    if (context.Properties.IssuedUtc is { } issuedUtc && now - issuedUtc >= RenewalInterval)
                    {
                        context.ShouldRenew = true;
                    }

                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToLogin = context =>
                    AccountErrors.SignInRequired.ToResult().ExecuteAsync(context.HttpContext);
                options.Events.OnRedirectToAccessDenied = context =>
                    TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context.HttpContext);
            });

        // Wired from DI rather than left to defaults, so tests can move the clock and every session lives in the database.
        services.AddOptions<CookieAuthenticationOptions>(Scheme)
            .Configure<DatabaseTicketStore, TimeProvider>((options, ticketStore, timeProvider) =>
            {
                options.SessionStore = ticketStore;
                options.TimeProvider = timeProvider;
            });

        // Fails closed: endpoints without their own policy need a session.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build());

        return services;
    }
}
