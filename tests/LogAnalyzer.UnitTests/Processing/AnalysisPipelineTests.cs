using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.UnitTests.Processing;

/// <summary>The run pipeline with a fake model and a fake recorder: at most two calls, attempts as they happen, and endings.</summary>
public sealed class AnalysisPipelineTests
{
    private static readonly Guid RunId = Guid.CreateVersion7();

    private static readonly string[] Lines =
    [
        "info: Orders.Api[0] Application started",
        "fail: Orders.Api[0] Database connection pool exhausted",
        "warn: Orders.Api[0] Connection pool at 90% of its limit",
    ];

    private const string Accepted =
        """
        {"summary":"The database ran out of connections.","findings":[{"title":"Database connection pool exhausted",
        "severity":"critical","category":"resource","line_refs":[2],"evidence":"Database connection pool exhausted",
        "explanation":"Requests wait for a connection.","suggested_action":"Check the pool size."}]}
        """;

    // Line 9 is past the end of the 3-line log.
    private const string LineOutOfRange =
        """
        {"summary":"The database ran out of connections.","findings":[{"title":"Database connection pool exhausted",
        "severity":"critical","category":"resource","line_refs":[9],"evidence":"Database connection pool exhausted",
        "explanation":"Requests wait for a connection.","suggested_action":"Check the pool size."}]}
        """;

    private static readonly DateTimeOffset Start = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);

    private readonly FakeRunRecorder _recorder = new();

    private readonly FakeAnalysisModel _model;

    public AnalysisPipelineTests() => _model = new FakeAnalysisModel(_clock);

    [Fact]
    public async Task Process_CompletesRunWhenTheFirstResponseIsAccepted()
    {
        _model.Reply(Accepted);

        var outcome = await ProcessAsync();

        Assert.True(outcome.IsCompleted);
        var completed = Assert.IsType<RunCompleted>(Assert.Single(_recorder.Calls));
        AssertAttempt(completed.Attempt, sequence: 1, Start, Start.AddSeconds(1));
        Assert.Empty(completed.Attempt.Violations);
        Assert.Equal("The database ran out of connections.", completed.Analysis.Summary);
        var request = Assert.Single(_model.Requests);
        Assert.Equal(RunId, request.RunId);
        Assert.Equal([new(1, Lines[0]), new(2, Lines[1]), new(3, Lines[2])], request.Lines);
        Assert.Null(request.Correction);
    }

    [Fact]
    public async Task Process_RecordsRejectionAtOnceThenSendsCorrection()
    {
        _model.Reply(LineOutOfRange);
        _model.Reply(Accepted);

        var outcome = await ProcessAsync();

        Assert.True(outcome.IsCompleted);
        Assert.Equal(2, _recorder.Calls.Count);
        var rejected = Assert.IsType<RejectedAttemptRecorded>(_recorder.Calls[0]);
        AssertAttempt(rejected.Attempt, sequence: 1, Start, Start.AddSeconds(1));
        Assert.Equal(
            [(ReasonCodes.LineOutOfRange, "findings[0].line_refs[0]")],
            rejected.Attempt.Violations.Select(violation => (violation.Code, violation.Path)));
        var completed = Assert.IsType<RunCompleted>(_recorder.Calls[1]);
        AssertAttempt(completed.Attempt, sequence: 2, Start.AddSeconds(1), Start.AddSeconds(2));

        Assert.Equal(2, _model.Requests.Count);
        var correction = _model.Requests[1].Correction;
        Assert.NotNull(correction);
        Assert.Equal(LineOutOfRange, correction.RejectedResponseText);
        Assert.Equal(rejected.Attempt.Violations, correction.Violations);
        Assert.All(correction.Violations, violation => Assert.False(string.IsNullOrWhiteSpace(violation.Message)));
        Assert.Equal(_model.Requests[0].Lines, _model.Requests[1].Lines);
    }

    [Fact]
    public async Task Process_FailsWithValidationFailedAfterTwoRejections()
    {
        _model.Reply(LineOutOfRange);
        _model.Reply("not json");
        _model.Reply(Accepted);

        var outcome = await ProcessAsync();

        Assert.Equal(FailureReasons.ValidationFailed, outcome.FailureReason);
        Assert.Equal(2, _model.Requests.Count);
        Assert.Equal(2, _recorder.Calls.Count);
        Assert.IsType<RejectedAttemptRecorded>(_recorder.Calls[0]);
        var failed = Assert.IsType<RunFailed>(_recorder.Calls[1]);
        Assert.Equal(FailureReasons.ValidationFailed, failed.FailureReason);
        Assert.NotNull(failed.Attempt);
        AssertAttempt(failed.Attempt, sequence: 2, Start.AddSeconds(1), Start.AddSeconds(2));
        Assert.Equal([ReasonCodes.NotJson], failed.Attempt.Violations.Select(violation => violation.Code));
    }

    [Theory]
    [InlineData(StopReason.OutputLimit, "TRUNCATED")]
    [InlineData(StopReason.Refusal, "REFUSED")]
    public async Task Process_SendsCorrectionAfterIncompleteResponse(StopReason stopReason, string code)
    {
        _model.Reply("{\"summary\":\"The data", stopReason);
        _model.Reply(Accepted);

        var outcome = await ProcessAsync();

        Assert.True(outcome.IsCompleted);
        Assert.Equal([code], _model.Requests[1].Correction!.Violations.Select(violation => violation.Code));
    }

    [Theory]
    [InlineData(FailureReasons.ModelUnavailable)]
    [InlineData(FailureReasons.ModelConfigurationError)]
    public async Task Process_FailsWithTheReasonOfAModelCallWithoutResponse(string failureReason)
    {
        _model.Throw(new ModelCallException(failureReason, "No response."));

        var outcome = await ProcessAsync();

        Assert.Equal(failureReason, outcome.FailureReason);
        // Passed on so the caller logs the cause, which the failure reason alone does not tell.
        Assert.IsType<ModelCallException>(outcome.Error);
        var failed = Assert.IsType<RunFailed>(Assert.Single(_recorder.Calls));
        Assert.Equal(failureReason, failed.FailureReason);
        Assert.Null(failed.Attempt);
    }

    [Fact]
    public async Task Process_KeepsTheRejectedAttemptWhenTheCorrectionCallFails()
    {
        _model.Reply(LineOutOfRange);
        _model.Throw(new ModelCallException(FailureReasons.ModelUnavailable, "Timed out."));

        var outcome = await ProcessAsync();

        Assert.Equal(FailureReasons.ModelUnavailable, outcome.FailureReason);
        Assert.Equal(2, _recorder.Calls.Count);
        Assert.IsType<RejectedAttemptRecorded>(_recorder.Calls[0]);
        var failed = Assert.IsType<RunFailed>(_recorder.Calls[1]);
        Assert.Equal(FailureReasons.ModelUnavailable, failed.FailureReason);
        Assert.Null(failed.Attempt);
    }

    // At a graceful shutdown the model call is cancelled; the run is then failed with a token that still works.
    [Fact]
    public async Task Process_FailsAsInterruptedWhenStopped()
    {
        using var stopping = new CancellationTokenSource();
        _model.Then(cancellationToken =>
        {
            stopping.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The call was not cancelled.");
        });

        var outcome = await new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, stopping.Token);

        Assert.Equal(FailureReasons.Interrupted, outcome.FailureReason);
        var failed = Assert.IsType<RunFailed>(Assert.Single(_recorder.Calls));
        Assert.Equal(FailureReasons.Interrupted, failed.FailureReason);
        Assert.False(failed.TokenWasCancelled);
    }

    // A response that arrived before the shutdown was evaluated, so it is recorded, and an accepted one completes the run.
    [Fact]
    public async Task Process_CompletesRunWhenStoppedAfterAnAcceptedResponseArrived()
    {
        using var stopping = new CancellationTokenSource();
        _model.Then(_ => StopThenReply(stopping, Accepted));

        var outcome = await new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, stopping.Token);

        Assert.True(outcome.IsCompleted);
        var completed = Assert.IsType<RunCompleted>(Assert.Single(_recorder.Calls));
        Assert.False(completed.TokenWasCancelled);
    }

    // The rejected response is recorded, but no corrective call starts once the API is stopping.
    [Fact]
    public async Task Process_RecordsRejectionThenStopsWhenStoppedAfterItArrived()
    {
        using var stopping = new CancellationTokenSource();
        _model.Then(_ => StopThenReply(stopping, LineOutOfRange));
        _model.Reply(Accepted);

        var outcome = await new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, stopping.Token);

        Assert.Equal(FailureReasons.Interrupted, outcome.FailureReason);
        Assert.Single(_model.Requests);
        Assert.Equal(2, _recorder.Calls.Count);
        var rejected = Assert.IsType<RejectedAttemptRecorded>(_recorder.Calls[0]);
        Assert.False(rejected.TokenWasCancelled);
        var failed = Assert.IsType<RunFailed>(_recorder.Calls[1]);
        Assert.Equal(FailureReasons.Interrupted, failed.FailureReason);
        Assert.False(failed.TokenWasCancelled);
    }

    [Fact]
    public async Task Process_RecordsSecondRejectionWhenStoppedAfterItArrived()
    {
        using var stopping = new CancellationTokenSource();
        _model.Reply(LineOutOfRange);
        _model.Then(_ => StopThenReply(stopping, LineOutOfRange));

        var outcome = await new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, stopping.Token);

        Assert.Equal(FailureReasons.ValidationFailed, outcome.FailureReason);
        var failed = Assert.IsType<RunFailed>(_recorder.Calls[1]);
        Assert.Equal(FailureReasons.ValidationFailed, failed.FailureReason);
        Assert.NotNull(failed.Attempt);
        Assert.False(failed.TokenWasCancelled);
    }

    // Writes get five seconds after a stop: a write that hangs longer is given up, and the run is failed as interrupted.
    [Fact]
    public async Task Process_GivesUpOnAHangingWriteFiveSecondsAfterAStop()
    {
        using var stopping = new CancellationTokenSource();
        _recorder.CompletionHangs = true;
        _model.Then(_ => StopThenReply(stopping, Accepted));

        var processing = new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, stopping.Token);
        _clock.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(processing.IsCompleted);
        _clock.Advance(TimeSpan.FromSeconds(0.2));
        var outcome = await processing.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(FailureReasons.Interrupted, outcome.FailureReason);
        var failed = Assert.IsType<RunFailed>(_recorder.Calls[^1]);
        Assert.Equal(FailureReasons.Interrupted, failed.FailureReason);
        Assert.True(failed.TokenWasCancelled);
    }

    // When even the failure cannot be written, the caller still learns the reason and the cause, to log them and retry.
    [Fact]
    public async Task Process_PassesOnReasonAndCauseWhenTheFailureCannotBeRecorded()
    {
        var cause = new ModelCallException(FailureReasons.ModelConfigurationError, "The API key was refused.");
        var writeError = new InvalidOperationException("The database is down.");
        _model.Throw(cause);
        _recorder.FailureWriteError = writeError;

        var exception = await Assert.ThrowsAsync<RunOutcomeNotRecordedException>(ProcessAsync);

        Assert.Equal(FailureReasons.ModelConfigurationError, exception.Outcome.FailureReason);
        Assert.Same(cause, exception.Outcome.Error);
        Assert.Same(writeError, exception.InnerException);
    }

    [Fact]
    public async Task Process_PassesOnValidationFailedWhenItCannotBeRecorded()
    {
        _model.Reply(LineOutOfRange);
        _model.Reply(LineOutOfRange);
        _recorder.FailureWriteError = new InvalidOperationException("The database is down.");

        var exception = await Assert.ThrowsAsync<RunOutcomeNotRecordedException>(ProcessAsync);

        Assert.Equal(FailureReasons.ValidationFailed, exception.Outcome.FailureReason);
    }

    [Fact]
    public async Task Process_FailsWithInternalErrorOnAnUnexpectedException()
    {
        var error = new InvalidOperationException("Unexpected.");
        _model.Throw(error);

        var outcome = await ProcessAsync();

        Assert.Equal(FailureReasons.InternalError, outcome.FailureReason);
        Assert.Same(error, outcome.Error);
        var failed = Assert.IsType<RunFailed>(Assert.Single(_recorder.Calls));
        Assert.Equal(FailureReasons.InternalError, failed.FailureReason);
    }

    // Without a cancelled token, an OperationCanceledException is not a shutdown, so the run is not reported as interrupted.
    [Fact]
    public async Task Process_TreatsCancellationItDidNotAskForAsInternalError()
    {
        _model.Throw(new OperationCanceledException("A timeout inside the adapter."));

        var outcome = await ProcessAsync();

        Assert.Equal(FailureReasons.InternalError, outcome.FailureReason);
    }

    private Task<RunOutcome> ProcessAsync() =>
        new AnalysisPipeline(_model, _recorder, _clock).ProcessAsync(RunId, Lines, CancellationToken.None);

    // The model has answered when the shutdown begins.
    private static ModelReply StopThenReply(CancellationTokenSource stopping, string text)
    {
        stopping.Cancel();
        return new ModelReply(text, StopReason.Completed, FakeAnalysisModel.Model, FakeAnalysisModel.PromptVersion);
    }

    private static void AssertAttempt(AttemptRecord attempt, int sequence, DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        Assert.Equal(sequence, attempt.Sequence);
        Assert.Equal(FakeAnalysisModel.Model, attempt.Model);
        Assert.Equal(FakeAnalysisModel.PromptVersion, attempt.PromptVersion);
        Assert.Equal(startedAt, attempt.StartedAt);
        Assert.Equal(finishedAt, attempt.FinishedAt);
    }
}
