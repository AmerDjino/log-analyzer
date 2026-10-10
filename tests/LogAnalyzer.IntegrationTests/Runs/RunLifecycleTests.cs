using System.Text.Json;
using LogAnalyzer.Core.Processing;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class RunLifecycleTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "analysis-history/run-completes")]
    public async Task Process_CompletesRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);
        var submittedAt = factory.Clock.GetUtcNow();

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var startedAt = run.GetProperty("startedAt").GetDateTimeOffset();
        Assert.True(startedAt >= submittedAt, $"Started at {startedAt:O}, before it was submitted at {submittedAt:O}.");
        // The scripted call takes one second on the test clock.
        Assert.Equal(startedAt.AddSeconds(1), run.GetProperty("finishedAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("failureReason").ValueKind);
        Assert.Equal(
            "The orders database ran out of connections, and a call to the inventory service timed out.",
            run.GetProperty("summary").GetString());
        Assert.Equal(
            ["Database connection pool exhausted", "Inventory service timed out"],
            run.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("title").GetString()));
    }

    [Fact]
    [Trait("Scenario", "analysis-history/failed-run-carries-its-reason")]
    public async Task Process_FailedRunCarriesItsReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.CitesLinePastTheEnd());
        factory.Model.Reply(runId, ModelReplies.CitesLinePastTheEnd());

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.ValidationFailed, run.GetProperty("failureReason").GetString());
        Assert.Equal(JsonValueKind.String, run.GetProperty("finishedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("summary").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("findings").ValueKind);
        Assert.Equal(40, run.GetProperty("statistics").GetProperty("lineCount").GetInt32());
    }

    [Fact]
    [Trait("Scenario", "analysis-history/model-failure-fails-the-run-with-its-reason")]
    public async Task Process_FailsRunWhenModelIsUnavailable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Fail(runId, FailureReasons.ModelUnavailable);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.ModelUnavailable, run.GetProperty("failureReason").GetString());
        // A call that yields no response has nothing to validate, so it is not an attempt.
        Assert.Empty(await database.ReadAttemptsAsync(runId, cancellationToken));
    }

    [Fact]
    public async Task Process_FailsRunWithModelConfigurationError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Fail(runId, FailureReasons.ModelConfigurationError);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.ModelConfigurationError, run.GetProperty("failureReason").GetString());
    }

    // Once claimed, a run must end even when processing fails before the model is asked, or it would stay running.
    [Fact]
    public async Task Process_FailsClaimedRunWhoseLinesCannotBeRead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await database.InsertQueuedRunAsync(
            person.Id, ["fail: Orders.Api[0] Order 1003 could not be stored"], factory.Clock.GetUtcNow(), cancellationToken,
            withLines: false);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.InternalError, run.GetProperty("failureReason").GetString());
        Assert.Empty(factory.Model.RequestsFor(runId));
    }

    // When the pipeline cannot write the failure, the processor writes it another way, with the same reason.
    [Fact]
    public async Task Process_FailsRunWithItsReasonWhenTheRecorderCannotWriteIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Fail(runId, FailureReasons.ModelUnavailable);
        factory.RunsWhoseFailureCannotBeRecorded.TryAdd(runId, true);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.ModelUnavailable, run.GetProperty("failureReason").GetString());
    }

    // PostgreSQL text cannot hold U+0000, which the validator does not reject, so it is left out rather than losing the
    // accepted analysis. Evidence cannot contain one: it must appear on a cited line, and logs with NUL are refused.
    [Fact]
    public async Task Process_StoresAcceptedTextWithoutNulCharacters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(
            runId,
            ModelReplies.Analysis(
                "The database ran out\u0000 of connections.",
                ModelReplies.Finding(
                    "Pool\u0000 exhausted",
                    "critical",
                    "resource",
                    [12],
                    "Database connection pool exhausted",
                    explanation: "Requests\u0000 wait.",
                    suggestedAction: "Check\u0000 the pool.")));

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal("The database ran out of connections.", run.GetProperty("summary").GetString());
        var finding = Assert.Single(run.GetProperty("findings").EnumerateArray());
        Assert.Equal("Pool exhausted", finding.GetProperty("title").GetString());
        Assert.Equal("Requests wait.", finding.GetProperty("explanation").GetString());
        Assert.Equal("Check the pool.", finding.GetProperty("suggestedAction").GetString());
        Assert.Equal(["accepted"], (await database.ReadAttemptsAsync(runId, cancellationToken)).Select(attempt => attempt.Outcome));
    }

    // The model gets every line with its number, starting at 1.
    [Fact]
    public async Task Process_SendsEveryLineWithItsNumber()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var log = FixtureLogs.Read(FixtureLogs.OrdersApi40);
        var runId = await runs.SubmitAcceptedAsync(log, "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var request = Assert.Single(factory.Model.RequestsFor(runId));
        Assert.Null(request.Correction);
        Assert.Equal(
            log.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n').Select((text, index) => new NumberedLine(index + 1, text)),
            request.Lines);
    }
}
