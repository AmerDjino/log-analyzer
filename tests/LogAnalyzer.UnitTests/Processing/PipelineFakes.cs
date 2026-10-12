using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.UnitTests.Processing;

/// <summary>A model that answers from a script, records each request, and takes one second per call on the test clock.</summary>
internal sealed class FakeAnalysisModel(FakeTimeProvider clock) : IAnalysisModel
{
    public const string Model = "fake";

    public const string PromptVersion = "fake-v1";

    private readonly Queue<Func<CancellationToken, ModelReply>> _script = new();

    public string ConfiguredModel => Model;

    public string ConfiguredPromptVersion => PromptVersion;

    public List<AnalysisRequest> Requests { get; } = [];

    public void Reply(string text, StopReason stopReason = StopReason.Completed, ModelUsage? usage = null) =>
        _script.Enqueue(_ => new ModelReply(text, stopReason, Model, PromptVersion) { Usage = usage ?? ModelUsage.None });

    public void Throw(Exception exception) => _script.Enqueue(_ => throw exception);

    public void Then(Func<CancellationToken, ModelReply> step) => _script.Enqueue(step);

    public Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        clock.Advance(TimeSpan.FromSeconds(1));
        return Task.FromResult(_script.Dequeue()(cancellationToken));
    }
}

/// <summary>Records what the pipeline asks to store, in order, and whether the token it got was already cancelled.</summary>
internal sealed class FakeRunRecorder : IRunRecorder
{
    public List<RecordedCall> Calls { get; } = [];

    /// <summary>When set, completing a run waits until its token is cancelled, like a write to a database that hangs.</summary>
    public bool CompletionHangs { get; set; }

    /// <summary>When set, failing a run throws this, like a write to a database that is down.</summary>
    public Exception? FailureWriteError { get; set; }

    public Task RecordRejectedAttemptAsync(Guid runId, AttemptRecord attempt, CancellationToken cancellationToken)
    {
        Calls.Add(new RejectedAttemptRecorded(runId, attempt, cancellationToken.IsCancellationRequested));
        return Task.CompletedTask;
    }

    public Task CompleteAsync(Guid runId, AttemptRecord acceptedAttempt, Analysis analysis, CancellationToken cancellationToken)
    {
        Calls.Add(new RunCompleted(runId, acceptedAttempt, analysis, cancellationToken.IsCancellationRequested));
        return CompletionHangs ? Task.Delay(Timeout.Infinite, cancellationToken) : Task.CompletedTask;
    }

    public Task FailAsync(Guid runId, string failureReason, AttemptRecord? rejectedAttempt, CancellationToken cancellationToken)
    {
        Calls.Add(new RunFailed(runId, failureReason, rejectedAttempt, cancellationToken.IsCancellationRequested));
        return FailureWriteError is { } error ? Task.FromException(error) : Task.CompletedTask;
    }
}

internal abstract record RecordedCall(Guid RunId);

internal sealed record RejectedAttemptRecorded(Guid RunId, AttemptRecord Attempt, bool TokenWasCancelled)
    : RecordedCall(RunId);

internal sealed record RunCompleted(Guid RunId, AttemptRecord Attempt, Analysis Analysis, bool TokenWasCancelled)
    : RecordedCall(RunId);

internal sealed record RunFailed(Guid RunId, string FailureReason, AttemptRecord? Attempt, bool TokenWasCancelled)
    : RecordedCall(RunId);
