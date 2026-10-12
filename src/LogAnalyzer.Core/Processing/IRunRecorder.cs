using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Core.Processing;

/// <summary>One model response the validator evaluated.</summary>
/// <param name="Sequence">1 for the first response, 2 for the corrected one.</param>
/// <param name="Violations">Why the response was rejected; empty when it was accepted.</param>
/// <param name="Usage">What the call that returned the response cost.</param>
public sealed record AttemptRecord(
    int Sequence,
    string Model,
    string PromptVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    IReadOnlyList<Violation> Violations,
    ModelUsage Usage)
{
    public bool IsAccepted => Violations.Count == 0;
}

/// <summary>
/// Where <see cref="AnalysisPipeline"/> records a run's progress as it happens. Each method is one transaction.
/// Infrastructure implements it with the database.
/// </summary>
public interface IRunRecorder
{
    /// <summary>Records a rejected attempt as soon as it is known, so a crash cannot lose the record of a call.</summary>
    Task RecordRejectedAttemptAsync(Guid runId, AttemptRecord attempt, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the run with the accepted analysis: its findings in the model's order, its summary and its highest
    /// severity, with the accepted attempt.
    /// </summary>
    Task CompleteAsync(Guid runId, AttemptRecord acceptedAttempt, Analysis analysis, CancellationToken cancellationToken);

    /// <summary>
    /// Fails the run with one of the <see cref="FailureReasons"/>, with its last rejected attempt when there is one.
    /// Attempts recorded before are kept.
    /// </summary>
    Task FailAsync(Guid runId, string failureReason, AttemptRecord? rejectedAttempt, CancellationToken cancellationToken);
}
