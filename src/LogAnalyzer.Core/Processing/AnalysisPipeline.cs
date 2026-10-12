using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Core.Processing;

/// <summary>How processing a run ended: completed, or failed with one of the <see cref="FailureReasons"/>.</summary>
/// <param name="Error">
/// The exception behind the failure, for the caller to log: a <see cref="ModelCallException"/>, or the unexpected
/// exception behind INTERNAL_ERROR. Null otherwise.
/// </param>
public sealed record RunOutcome(string? FailureReason, Exception? Error = null)
{
    public static RunOutcome Completed { get; } = new(FailureReason: null);

    public bool IsCompleted => FailureReason is null;
}

/// <summary>
/// The pipeline knew how the run ends, but recording the failure failed too, for example because the database is down.
/// <see cref="Outcome"/> holds the reason and its cause; the inner exception is the failed write.
/// </summary>
public sealed class RunOutcomeNotRecordedException(RunOutcome outcome, Exception writeError)
    : Exception($"The run's failure, {outcome.FailureReason}, could not be recorded.", writeError)
{
    public RunOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Analyzes a claimed run's log with the model and records how the run ends. It makes at most two model calls: every
/// line, then, if the validator rejects that response for any reason, the same lines with a correction. Each attempt is
/// recorded as it happens, and findings are stored only from an accepted response.
/// </summary>
public sealed class AnalysisPipeline(IAnalysisModel model, IRunRecorder recorder, TimeProvider timeProvider)
{
    public const int MaxAttempts = 2;

    // How long writes may still take once the API starts stopping: long enough to record a response that has arrived,
    // or the run's failure, and short enough to finish before the host's shutdown timeout.
    private static readonly TimeSpan ShutdownWriteTimeout = TimeSpan.FromSeconds(5);

    /// <param name="lines">The run's lines: line N is <c>lines[N - 1]</c>.</param>
    /// <param name="cancellationToken">
    /// Cancelled at a graceful shutdown. It cancels the model call, and no new call starts; the run then fails with
    /// INTERRUPTED. Writes are not cancelled with it, so a response that has arrived is still recorded.
    /// </param>
    public async Task<RunOutcome> ProcessAsync(Guid runId, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        using var writes = new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider);
        using var shutdown = cancellationToken.Register(() => writes.CancelAfter(ShutdownWriteTimeout));
        try
        {
            return await AnalyzeAsync(runId, lines, cancellationToken, writes.Token);
        }
        catch (ModelCallException exception)
        {
            return await FailAsync(
                runId, new RunOutcome(exception.FailureReason, exception), rejectedAttempt: null, writes.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await FailAsync(runId, new RunOutcome(FailureReasons.Interrupted), rejectedAttempt: null, writes.Token);
        }
        catch (Exception exception) when (exception is not RunOutcomeNotRecordedException)
        {
            return await FailAsync(
                runId, new RunOutcome(FailureReasons.InternalError, exception), rejectedAttempt: null, writes.Token);
        }
    }

    private async Task<RunOutcome> AnalyzeAsync(
        Guid runId, IReadOnlyList<string> lines, CancellationToken stoppingToken, CancellationToken writeToken)
    {
        var numberedLines = new NumberedLine[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            numberedLines[index] = new NumberedLine(index + 1, lines[index]);
        }

        Correction? correction = null;
        for (var sequence = 1; ; sequence++)
        {
            // No call starts once the API is stopping.
            stoppingToken.ThrowIfCancellationRequested();
            var startedAt = timeProvider.GetUtcNow();
            var reply = await model.AnalyzeAsync(new AnalysisRequest(runId, numberedLines, correction), stoppingToken);
            var finishedAt = timeProvider.GetUtcNow();

            var result = ResponseValidator.Validate(new ValidationInput(reply.Text, reply.StopReason, lines));
            var attempt = new AttemptRecord(
                sequence, reply.Model, reply.PromptVersion, startedAt, finishedAt, result.Violations, reply.Usage);
            if (result.IsAccepted)
            {
                await recorder.CompleteAsync(runId, attempt, result.Analysis, writeToken);
                return RunOutcome.Completed;
            }

            if (sequence == MaxAttempts)
            {
                // Nothing from either response is stored.
                return await FailAsync(runId, new RunOutcome(FailureReasons.ValidationFailed), attempt, writeToken);
            }

            await recorder.RecordRejectedAttemptAsync(runId, attempt, writeToken);
            // The response text lives only here, in memory, for the corrective request.
            correction = new Correction(reply.Text, result.Violations);
        }
    }

    // When the failure cannot be written either, the reason and its cause travel with the write's error, so the caller
    // can log both and fail the run another way with the same reason.
    private async Task<RunOutcome> FailAsync(
        Guid runId, RunOutcome outcome, AttemptRecord? rejectedAttempt, CancellationToken writeToken)
    {
        try
        {
            await recorder.FailAsync(runId, outcome.FailureReason!, rejectedAttempt, writeToken);
            return outcome;
        }
        catch (Exception exception)
        {
            throw new RunOutcomeNotRecordedException(outcome, exception);
        }
    }
}
