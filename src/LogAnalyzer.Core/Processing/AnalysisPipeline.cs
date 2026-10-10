using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Core.Processing;

/// <summary>How processing a run ended: completed, or failed with one of the <see cref="FailureReasons"/>.</summary>
/// <param name="Error">The unexpected exception behind INTERNAL_ERROR, for the caller to log; null otherwise.</param>
public sealed record RunOutcome(string? FailureReason, Exception? Error = null)
{
    public static RunOutcome Completed { get; } = new(FailureReason: null);

    public bool IsCompleted => FailureReason is null;
}

/// <summary>
/// Analyzes a claimed run's log with the model and records how the run ends. It makes at most two model calls: every
/// line, then, if the validator rejects that response for any reason, the same lines with a correction. Each attempt is
/// recorded as it happens, and findings are stored only from an accepted response.
/// </summary>
public sealed class AnalysisPipeline(IAnalysisModel model, IRunRecorder recorder, TimeProvider timeProvider)
{
    public const int MaxAttempts = 2;

    // Long enough to record the failure, short enough to finish before the host's shutdown timeout.
    private static readonly TimeSpan InterruptedWriteTimeout = TimeSpan.FromSeconds(5);

    /// <param name="lines">The run's lines: line N is <c>lines[N - 1]</c>.</param>
    /// <param name="cancellationToken">Cancelled at a graceful shutdown; the run then fails with INTERRUPTED.</param>
    public async Task<RunOutcome> ProcessAsync(Guid runId, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        try
        {
            return await AnalyzeAsync(runId, lines, cancellationToken);
        }
        catch (ModelCallException exception)
        {
            await recorder.FailAsync(runId, exception.FailureReason, rejectedAttempt: null, cancellationToken);
            return new RunOutcome(exception.FailureReason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The stopping token is already cancelled, so the failure is written with a deadline of its own.
            using var deadline = new CancellationTokenSource(InterruptedWriteTimeout, timeProvider);
            await recorder.FailAsync(runId, FailureReasons.Interrupted, rejectedAttempt: null, deadline.Token);
            return new RunOutcome(FailureReasons.Interrupted);
        }
        catch (Exception exception)
        {
            await recorder.FailAsync(runId, FailureReasons.InternalError, rejectedAttempt: null, cancellationToken);
            return new RunOutcome(FailureReasons.InternalError, exception);
        }
    }

    private async Task<RunOutcome> AnalyzeAsync(Guid runId, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        var numberedLines = lines.Select((text, index) => new NumberedLine(index + 1, text)).ToList();
        Correction? correction = null;
        for (var sequence = 1; ; sequence++)
        {
            var startedAt = timeProvider.GetUtcNow();
            var reply = await model.AnalyzeAsync(new AnalysisRequest(runId, numberedLines, correction), cancellationToken);
            var finishedAt = timeProvider.GetUtcNow();

            var result = ResponseValidator.Validate(new ValidationInput(reply.Text, reply.StopReason, lines));
            var attempt = new AttemptRecord(sequence, reply.Model, reply.PromptVersion, startedAt, finishedAt, result.Violations);
            if (result.IsAccepted)
            {
                await recorder.CompleteAsync(runId, attempt, result.Analysis, cancellationToken);
                return RunOutcome.Completed;
            }

            if (sequence == MaxAttempts)
            {
                // Nothing from either response is stored.
                await recorder.FailAsync(runId, FailureReasons.ValidationFailed, attempt, cancellationToken);
                return new RunOutcome(FailureReasons.ValidationFailed);
            }

            await recorder.RecordRejectedAttemptAsync(runId, attempt, cancellationToken);
            // The response text lives only here, in memory, for the corrective request.
            correction = new Correction(reply.Text, result.Violations);
        }
    }
}
