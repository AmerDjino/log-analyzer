namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>Whether pending migrations have been applied. Read by the database health check.</summary>
public sealed class DatabaseMigrationState
{
    private volatile bool _isApplied;

    public bool IsApplied => _isApplied;

    internal void MarkApplied() => _isApplied = true;
}
