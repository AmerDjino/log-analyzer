using System.Security.Claims;
using LogAnalyzer.Infrastructure.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LogAnalyzer.Api.Accounts;

/// <summary>
/// Keeps authentication tickets in the user_sessions table, so the session cookie carries only a protected session id
/// and deleting the row ends the session. The cookie handler always calls the overloads that receive the HttpContext.
/// </summary>
internal sealed class DatabaseTicketStore(TimeProvider timeProvider) : ITicketStore
{
    private static readonly TicketSerializer Serializer = TicketSerializer.Default;

    public async Task<string> StoreAsync(
        AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("A session ticket needs the user's id."));
        var id = await Sessions(httpContext).CreateAsync(
            userId, Serializer.Serialize(ticket), ExpiresAt(ticket), cancellationToken);
        return id.ToString();
    }

    public Task RenewAsync(
        string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken) =>
        Guid.TryParse(key, out var id)
            ? Sessions(httpContext).RenewAsync(id, Serializer.Serialize(ticket), ExpiresAt(ticket), cancellationToken)
            : Task.CompletedTask;

    public async Task<AuthenticationTicket?> RetrieveAsync(
        string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
        {
            return null;
        }

        var ticket = await Sessions(httpContext).FindActiveAsync(id, cancellationToken);
        return ticket is null ? null : Serializer.Deserialize(ticket);
    }

    public Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken) =>
        Guid.TryParse(key, out var id)
            ? Sessions(httpContext).RemoveAsync(id, cancellationToken)
            : Task.CompletedTask;

    public Task<string> StoreAsync(AuthenticationTicket ticket) => throw HttpContextRequired();

    public Task RenewAsync(string key, AuthenticationTicket ticket) => throw HttpContextRequired();

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => throw HttpContextRequired();

    public Task RemoveAsync(string key) => throw HttpContextRequired();

    private static UserSessionRepository Sessions(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<UserSessionRepository>();

    private DateTimeOffset ExpiresAt(AuthenticationTicket ticket) =>
        ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().Add(AccountSessions.IdleTimeout);

    private static NotSupportedException HttpContextRequired() =>
        new("The session store is used through the overloads that receive the HttpContext.");
}
