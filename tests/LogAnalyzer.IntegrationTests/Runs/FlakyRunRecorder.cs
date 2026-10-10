using System.Collections.Concurrent;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Processing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The real recorder, except that failing one of the given runs throws, as if the database failed at that moment.
/// </summary>
public sealed class FlakyRunRecorder(IRunRecorder recorder, ConcurrentDictionary<Guid, bool> runsWhoseFailureFails) : IRunRecorder
{
    public Task RecordRejectedAttemptAsync(Guid runId, AttemptRecord attempt, CancellationToken cancellationToken) =>
        recorder.RecordRejectedAttemptAsync(runId, attempt, cancellationToken);

    public Task CompleteAsync(Guid runId, AttemptRecord acceptedAttempt, Analysis analysis, CancellationToken cancellationToken) =>
        recorder.CompleteAsync(runId, acceptedAttempt, analysis, cancellationToken);

    public Task FailAsync(Guid runId, string failureReason, AttemptRecord? rejectedAttempt, CancellationToken cancellationToken) =>
        runsWhoseFailureFails.ContainsKey(runId)
            ? Task.FromException(new InvalidOperationException($"Scripted failure to record that run {runId} failed."))
            : recorder.FailAsync(runId, failureReason, rejectedAttempt, cancellationToken);
}
