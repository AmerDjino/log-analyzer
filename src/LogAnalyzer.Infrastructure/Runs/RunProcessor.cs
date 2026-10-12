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
    IAnalysisModel model,
    TimeProvider timeProvider,
    ILogger<RunProcessor> logger)
{
    // SKIP LOCKED lets several loops claim at the same time without waiting for one another. The claim records the
    // configured model and prompt version, which an identical resubmission is compared with.
    private const string ClaimSql =
        """
        UPDATE runs SET status = 'running', started_at = @now, model = @model, prompt_version = @promptVersion
        WHERE id = (SELECT id FROM runs WHERE status = 'queued'
                    ORDER BY submitted_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
        RETURNING id
        """;

    // How long failing a run may take when processing it went wrong: the database may be the problem.
    private static readonly TimeSpan FailWriteTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Claims and processes the oldest queued run. False when no run is queued. A claimed run always ends: if processing
    /// fails before the pipeline records an outcome, the run fails here, and if even that fails, the next start's sweep
    /// fails it.
    /// </summary>
    /// <param name="stoppingToken">Cancelled at a graceful shutdown; the run being processed then fails with INTERRUPTED.</param>
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        if (await ClaimNextAsync(stoppingToken) is not { } runId)
        {
            return false;
        }

        try
        {
            LogOutcome(runId, await ProcessClaimedAsync(runId, stoppingToken));
        }
        catch (RunOutcomeNotRecordedException exception)
        {
            LogOutcome(runId, exception.Outcome);
            LogNotRecorded(logger, exception.InnerException!, runId, exception.Outcome.FailureReason!);
            await TryFailAsync(runId, exception.Outcome.FailureReason!);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogFailed(logger, exception: null, runId, FailureReasons.Interrupted);
            await TryFailAsync(runId, FailureReasons.Interrupted);
        }
        catch (Exception exception)
        {
            LogUnrecorded(logger, exception, runId);
            await TryFailAsync(runId, FailureReasons.InternalError);
        }

        return true;
    }

    /// <summary>
    /// Fails every run still running, with INTERRUPTED: the API stopped while it processed them, and nothing resumes
    /// a run. Called when the worker starts, before it claims anything, so it assumes one API instance per database.
    /// </summary>
    public async Task FailInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE runs SET status = 'failed', failure_reason = @reason, finished_at = @now WHERE status = 'running'");
        command.Parameters.AddWithValue("reason", FailureReasons.Interrupted);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        var interrupted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (interrupted > 0)
        {
            LogInterrupted(logger, interrupted);
        }
    }

    // The cause of a failure is logged with it: the failure reason alone does not tell what went wrong.
    private void LogOutcome(Guid runId, RunOutcome outcome)
    {
        switch (outcome)
        {
            case { IsCompleted: true }:
                LogCompleted(logger, runId);
                break;
            case { FailureReason: FailureReasons.InternalError, Error: { } error }:
                LogInternalError(logger, error, runId);
                break;
            default:
                LogFailed(logger, outcome.Error, runId, outcome.FailureReason!);
                break;
        }
    }

    private async Task<RunOutcome> ProcessClaimedAsync(Guid runId, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var lines = await scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>().RunLogs
            .AsNoTracking()
            .Where(log => log.RunId == runId)
            .Select(log => log.Lines)
            .SingleAsync(stoppingToken);

        return await scope.ServiceProvider.GetRequiredService<AnalysisPipeline>().ProcessAsync(runId, lines, stoppingToken);
    }

    private async Task<Guid?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ClaimSql);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("model", model.ConfiguredModel);
        command.Parameters.AddWithValue("promptVersion", model.ConfiguredPromptVersion);
        return await command.ExecuteScalarAsync(cancellationToken) as Guid?;
    }

    // Best effort, with a deadline of its own. Only a run still running is failed, so an outcome already recorded stays.
    private async Task TryFailAsync(Guid runId, string failureReason)
    {
        try
        {
            using var deadline = new CancellationTokenSource(FailWriteTimeout, timeProvider);
            await using var command = dataSource.CreateCommand(
                """
                UPDATE runs SET status = 'failed', failure_reason = @reason, finished_at = @now
                WHERE id = @id AND status = 'running'
                """);
            command.Parameters.AddWithValue("id", runId);
            command.Parameters.AddWithValue("reason", failureReason);
            command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
            await command.ExecuteNonQueryAsync(deadline.Token);
        }
        catch (Exception exception)
        {
            LogStillRunning(logger, exception, runId);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {RunId} completed.")]
    private static partial void LogCompleted(ILogger logger, Guid runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {RunId} failed with {FailureReason}.")]
    private static partial void LogFailed(ILogger logger, Exception? exception, Guid runId, string failureReason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Run {RunId} failed with INTERNAL_ERROR.")]
    private static partial void LogInternalError(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Run {RunId} could not be recorded as failed with {FailureReason}; trying once more.")]
    private static partial void LogNotRecorded(ILogger logger, Exception exception, Guid runId, string failureReason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Processing run {RunId} failed before an outcome was recorded; failing it with INTERNAL_ERROR.")]
    private static partial void LogUnrecorded(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Run {RunId} could not be failed and stays running until the API starts again.")]
    private static partial void LogStillRunning(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed {Count} runs left running when the API stopped, with INTERRUPTED.")]
    private static partial void LogInterrupted(ILogger logger, int count);
}
