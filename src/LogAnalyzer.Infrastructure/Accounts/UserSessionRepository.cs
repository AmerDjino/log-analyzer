using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogAnalyzer.Infrastructure.Accounts;

/// <summary>Stores signed-in sessions. A session that has expired is deleted when it is presented.</summary>
public sealed class UserSessionRepository(LogAnalyzerDbContext database, TimeProvider timeProvider)
{
    public async Task<Guid> CreateAsync(
        Guid userId, byte[] ticket, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Ticket = ticket,
            ExpiresAt = expiresAt,
        };
        database.UserSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    /// <summary>The stored ticket of a session that has not expired, or null.</summary>
    public async Task<byte[]?> FindActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await database.UserSessions.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            await RemoveAsync(id, cancellationToken);
            return null;
        }

        return session.Ticket;
    }

    public Task RenewAsync(Guid id, byte[] ticket, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        database.UserSessions
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.Ticket, ticket).SetProperty(s => s.ExpiresAt, expiresAt),
                cancellationToken);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken) =>
        database.UserSessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
}
