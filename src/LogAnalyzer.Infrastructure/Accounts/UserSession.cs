namespace LogAnalyzer.Infrastructure.Accounts;

/// <summary>
/// A signed-in session. The cookie carries only the protected <see cref="Id"/>;
/// the serialized authentication ticket stays here, so deleting the row ends the session.
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required byte[] Ticket { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
