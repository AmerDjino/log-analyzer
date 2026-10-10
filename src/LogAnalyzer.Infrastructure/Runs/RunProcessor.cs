using LogAnalyzer.Core.Processing;
using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// Claims queued runs one at a time, oldest first, and analyzes each in a scope of its own. RunWorker calls it in its
/// loops; tests call it to drain the queue step by step. The claim is one statement, and no transaction or lock is held
/// while the model answers.
/// </summary>
public sealed partial class RunProcessor(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<RunProcessor> logger)
{
    // SKIP LOCKED lets several loops claim at the same time without waiting for one another.
    private const string ClaimSql =
        """
        UPDATE runs SET status = 'running', started_at = @now
        WHERE id = (SELECT id FROM runs WHERE status = 'queued'
                    ORDER BY submitted_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
        RETURNING id
        """;

    /// <summary>Claims and processes the oldest queued run. False when no run is queued.</summary>
    /// <param name="stoppingToken">Cancelled at a graceful shutdown; the run being processed then fails with INTERRUPTED.</param>
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        if (await ClaimNextAsync(stoppingToken) is not { } runId)
        {
            return false;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var lines = await scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>().RunLogs
            .AsNoTracking()
            .Where(log => log.RunId == runId)
            .Select(log => log.Lines)
            .SingleAsync(stoppingToken);

        var outcome = await scope.ServiceProvider.GetRequiredService<AnalysisPipeline>()
            .ProcessAsync(runId, lines, stoppingToken);
        if (outcome.Error is { } error)
        {
            LogInternalError(logger, error, runId);
        }
        else if (outcome.FailureReason is { } failureReason)
        {
            LogFailed(logger, runId, failureReason);
        }
        else
        {
            LogCompleted(logger, runId);
        }

        return true;
    }

    private async Task<Guid?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ClaimSql);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        return await command.ExecuteScalarAsync(cancellationToken) as Guid?;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} completed.")]
    private static partial void LogCompleted(ILogger logger, Guid runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} failed with {FailureReason}.")]
    private static partial void LogFailed(ILogger logger, Guid runId, string failureReason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed with INTERNAL_ERROR.")]
    private static partial void LogInternalError(ILogger logger, Exception exception, Guid runId);
}
