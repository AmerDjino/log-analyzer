namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>Whether pending migrations have been applied. Read by the database health check, and awaited by the run worker.</summary>
public sealed class DatabaseMigrationState
{
    private readonly TaskCompletionSource _applied = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsApplied => _applied.Task.IsCompleted;

    /// <summary>Completes once the migrations are applied.</summary>
    public Task WhenApplied => _applied.Task;

    internal void MarkApplied() => _applied.TrySetResult();
}
