using System.Text.Json;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class CorrectiveRetryTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "response-validation/corrected-response-is-accepted")]
    public async Task Process_AcceptsCorrectedResponse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.ParaphrasedEvidence);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var requests = factory.Model.RequestsFor(runId);
        Assert.Equal(2, requests.Count);
        Assert.Null(requests[0].Correction);
        var correction = requests[1].Correction;
        Assert.NotNull(correction);
        Assert.Equal(ModelReplies.ParaphrasedEvidence, correction.RejectedResponseText);
        var violation = Assert.Single(correction.Violations);
        Assert.Equal((ReasonCodes.EvidenceNotFound, "findings[0].evidence"), (violation.Code, violation.Path));
        Assert.False(string.IsNullOrWhiteSpace(violation.Message));
        Assert.Equal(requests[0].Lines, requests[1].Lines);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal(
            ["Database connection pool exhausted", "Inventory service timed out"],
            run.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("title").GetString()));
    }

    [Fact]
    [Trait("Scenario", "response-validation/second-rejection-fails-the-run")]
    public async Task Process_FailsRunAfterSecondRejection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.CitesLinePastTheEnd());
        factory.Model.Reply(runId, ModelReplies.ParaphrasedEvidence);
        // Never asked for: the run fails after the second response.
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        Assert.Equal(2, factory.Model.RequestsFor(runId).Count);
        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.ValidationFailed, run.GetProperty("failureReason").GetString());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("findings").ValueKind);
        Assert.Equal(0, await database.CountFindingsAsync(runId, cancellationToken));
        Assert.Equal(["rejected", "rejected"], (await database.ReadAttemptsAsync(runId, cancellationToken)).Select(attempt => attempt.Outcome));
    }

    // A truncated or refused response is rejected like any other, so it also gets one corrected attempt.
    [Theory]
    [InlineData(StopReason.OutputLimit, "TRUNCATED")]
    [InlineData(StopReason.Refusal, "REFUSED")]
    public async Task Process_RequestsCorrectionAfterIncompleteResponse(StopReason stopReason, string code)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(runId, "{\"summary\":\"The database", stopReason);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var correction = factory.Model.RequestsFor(runId)[1].Correction;
        Assert.Equal(code, Assert.Single(correction!.Violations).Code);
        Assert.Equal("completed", (await runs.ReadRunAsync(runId, cancellationToken)).GetProperty("status").GetString());
    }
}
