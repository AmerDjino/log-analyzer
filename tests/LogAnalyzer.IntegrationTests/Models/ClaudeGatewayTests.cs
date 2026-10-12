using System.Text.Json;
using System.Text.Json.Nodes;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;
using LogAnalyzer.IntegrationTests.Accounts;
using LogAnalyzer.IntegrationTests.Runs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>Runs analyzed by Claude, against the fake Messages API: the requests sent, and what attempts record.</summary>
public sealed class ClaudeGatewayTests(ClaudeApiFactory factory) : IClassFixture<ClaudeApiFactory>
{
    private static readonly string Log = FixtureLogs.Read(FixtureLogs.OrdersApi40);

    private static readonly string OtherLog = FixtureLogs.Read(FixtureLogs.Levels10);

    // The start of an analysis cut off at the output limit.
    private const string CutOff = """{"summary":"The database ran out of conn""";

    [Fact]
    [Trait("Scenario", "model-gateway/claude-is-used-when-selected")]
    public async Task Run_CallsModelServiceWhenClaudeIsSelected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (runId, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        var request = Assert.Single(factory.Messages.Requests);
        Assert.Equal(ClaudeTestSettings.ApiKey, request.Headers["x-api-key"]);
        Assert.Equal("claude-opus-5-5", request.Json.GetProperty("model").GetString());
        var attempt = Assert.Single(await database.ReadAttemptsAsync(runId, cancellationToken));
        Assert.Equal("accepted", attempt.Outcome);
        Assert.Equal("claude-opus-5-5", attempt.Model);
        Assert.Equal("log-analysis/v1", attempt.PromptVersion);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/key-never-leaks")]
    public async Task Run_NeverExposesApiKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);
        factory.Messages.Fail(401, "authentication_error");
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        // One run completes, and one fails because the service rejects the key.
        var completed = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        await factory.ProcessQueuedRunsAsync(cancellationToken);
        var failed = await runs.SubmitAcceptedAsync(OtherLog, fileName: null, cancellationToken);
        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var answers = new List<string>();
        foreach (var path in new[] { $"/runs/{completed}", $"/runs/{failed}", "/runs" })
        {
            using var response = await runs.Http.GetAsync(path, cancellationToken);
            answers.Add(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        Assert.Equal("failed", (await runs.ReadRunAsync(failed, cancellationToken)).GetProperty("status").GetString());
        Assert.All(answers, answer => Assert.DoesNotContain(ClaudeTestSettings.ApiKey, answer, StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(ClaudeTestSettings.ApiKey, StringComparison.Ordinal));
        Assert.Empty(await database.FindValueAsync(ClaudeTestSettings.ApiKey, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "model-gateway/output-limit-is-rejected-as-truncated")]
    public async Task Run_RejectsMaxTokensAsTruncated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(CutOff, stopReason: "max_tokens");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (runId, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        var attempts = await database.ReadAttemptsAsync(runId, cancellationToken);
        Assert.Equal(["rejected", "accepted"], attempts.Select(attempt => attempt.Outcome));
        Assert.Equal([ReasonCodes.Truncated], attempts[0].RejectionReasons.Select(reason => reason.Code));
        Assert.Equal(2, factory.Messages.Requests.Count);
        Assert.Contains(ReasonCodes.Truncated, LastUserText(factory.Messages.Requests[1]), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/refusal-is-rejected-as-refused")]
    public async Task Run_RejectsRefusalAsRefused()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(string.Empty, stopReason: "refusal");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (runId, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        var attempts = await database.ReadAttemptsAsync(runId, cancellationToken);
        Assert.Equal("rejected", attempts[0].Outcome);
        Assert.Equal([ReasonCodes.Refused], attempts[0].RejectionReasons.Select(reason => reason.Code));
    }

    // A complete, valid analysis is still rejected when the service reports a stop reason the adapter does not know.
    [Fact]
    [Trait("Scenario", "model-gateway/unexpected-stop-reason-is-never-accepted")]
    public async Task Run_RejectsUnknownStopReasonAsTruncated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis, stopReason: "pause_turn");
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (runId, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        var attempts = await database.ReadAttemptsAsync(runId, cancellationToken);
        Assert.Equal("rejected", attempts[0].Outcome);
        Assert.Equal([ReasonCodes.Truncated], attempts[0].RejectionReasons.Select(reason => reason.Code));
    }

    [Fact]
    [Trait("Scenario", "model-gateway/configured-version-is-used-and-recorded")]
    public async Task Run_RecordsConfiguredPromptVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);
        await using var v2 = factory.CreateHost(promptVersion: "log-analysis/v2");
        await using var database = new RunsDatabase(factory.ConnectionString);

        var runs = await SignInNewPersonAsync(v2, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        await ClaudeApiFactory.ProcessQueuedRunsAsync(v2, cancellationToken);

        var attempt = Assert.Single(await database.ReadAttemptsAsync(runId, cancellationToken));
        Assert.Equal("log-analysis/v2", attempt.PromptVersion);
        Assert.Equal("log-analysis/v2", (await database.ReadAnalyzedWithAsync(runId, cancellationToken)).PromptVersion);
        Assert.Equal("System prompt log-analysis/v2.", SystemText(Assert.Single(factory.Messages.Requests)));
    }

    [Fact]
    [Trait("Scenario", "model-gateway/past-runs-keep-their-version")]
    public async Task Runs_KeepTheirPromptVersionAfterConfigurationChange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);
        var (earlier, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        // The configuration changes: the API starts again with v2 and analyzes another run with it.
        await using var v2 = factory.CreateHost(promptVersion: "log-analysis/v2");
        var later = await (await SignInNewPersonAsync(v2, cancellationToken)).SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        await ClaudeApiFactory.ProcessQueuedRunsAsync(v2, cancellationToken);

        Assert.Equal("log-analysis/v2", Assert.Single(await database.ReadAttemptsAsync(later, cancellationToken)).PromptVersion);
        Assert.Equal("log-analysis/v1", Assert.Single(await database.ReadAttemptsAsync(earlier, cancellationToken)).PromptVersion);
        Assert.Equal("log-analysis/v1", (await database.ReadAnalyzedWithAsync(earlier, cancellationToken)).PromptVersion);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/usage-is-recorded")]
    public async Task Attempt_RecordsUsage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        var answerTime = TimeSpan.FromMilliseconds(300);
        factory.Messages.Answer(ModelReplies.OrdersAnalysis, delay: answerTime, requestId: "req_usage_check");

        var (runId, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var _ = database;

        var attempt = Assert.Single(await database.ReadAttemptsAsync(runId, cancellationToken));
        Assert.Equal(FakeMessagesApi.InputTokens, attempt.InputTokens);
        Assert.Equal(FakeMessagesApi.OutputTokens, attempt.OutputTokens);
        Assert.True(
            attempt.FinishedAt - attempt.StartedAt >= answerTime,
            $"Latency {attempt.FinishedAt - attempt.StartedAt} is shorter than the {answerTime} the answer took.");
        Assert.Equal("claude-opus-5-5", attempt.Model);
        Assert.Equal(0, attempt.TransportRetries);
        Assert.Equal("req_usage_check", attempt.RequestId);
    }

    [Fact]
    public async Task Request_CarriesTheContractFormatEffortModelAndLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (_, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var __ = database;

        var request = Assert.Single(factory.Messages.Requests).Json;
        Assert.Equal("claude-opus-5-5", request.GetProperty("model").GetString());
        Assert.Equal(16_000, request.GetProperty("max_tokens").GetInt32());
        var output = request.GetProperty("output_config");
        Assert.Equal("medium", output.GetProperty("effort").GetString());
        Assert.Equal("json_schema", output.GetProperty("format").GetProperty("type").GetString());
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(AnalysisContract.SchemaJson), JsonNode.Parse(output.GetProperty("format").GetProperty("schema").GetRawText())),
            "The output format's schema is not the contract's.");
        // No fallback model, and thinking stays the model's own adaptive mode.
        Assert.False(request.TryGetProperty("fallbacks", out _));
        Assert.False(request.TryGetProperty("thinking", out _));

        var system = Assert.Single(request.GetProperty("system").EnumerateArray());
        Assert.Equal("System prompt log-analysis/v1.", system.GetProperty("text").GetString());
        Assert.Equal("ephemeral", system.GetProperty("cache_control").GetProperty("type").GetString());
        var message = Assert.Single(request.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
        var log = Assert.Single(message.GetProperty("content").EnumerateArray());
        Assert.StartsWith("<log>\n1: ", log.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal("ephemeral", log.GetProperty("cache_control").GetProperty("type").GetString());
    }

    // The correction repeats the log block unchanged, so the second call reads it from the cache, and replays no
    // assistant turn.
    [Fact]
    public async Task CorrectiveRequest_IsOneUserTurnWithTheSameLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        factory.Messages.Answer(ModelReplies.CitesLinePastTheEnd());
        factory.Messages.Answer(ModelReplies.OrdersAnalysis);

        var (_, database) = await SubmitAndProcessAsync(Log, cancellationToken);
        await using var __ = database;

        Assert.Equal(2, factory.Messages.Requests.Count);
        var first = Assert.Single(factory.Messages.Requests[0].Json.GetProperty("messages").EnumerateArray());
        var second = Assert.Single(factory.Messages.Requests[1].Json.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", second.GetProperty("role").GetString());
        var blocks = second.GetProperty("content").EnumerateArray().ToList();
        Assert.Equal(2, blocks.Count);
        Assert.Equal(first.GetProperty("content")[0].GetRawText(), blocks[0].GetRawText());
        var correction = blocks[1].GetProperty("text").GetString()!;
        Assert.Contains("<rejected_response>", correction, StringComparison.Ordinal);
        Assert.Contains(ReasonCodes.LineOutOfRange, correction, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shutdown_FailsTheRunInProgressAsInterrupted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.Messages.Reset();
        // Answers within the timeout, but after the shutdown.
        factory.Messages.Answer(ModelReplies.OrdersAnalysis, delay: TimeSpan.FromSeconds(4));
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var processing = factory.ProcessQueuedRunsAsync(stopping.Token);
        await WaitUntilAsync(() => factory.Messages.Requests.Count == 1, cancellationToken);
        await stopping.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.Interrupted, run.GetProperty("failureReason").GetString());
    }

    private static async Task<RunsClient> SignInNewPersonAsync(WebApplicationFactory<Program> host, CancellationToken cancellationToken)
    {
        var accounts = new AccountsClient(host.CreateClient());
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);
        return new RunsClient(accounts.Http);
    }

    private static string SystemText(RecordedRequest request) =>
        request.Json.GetProperty("system")[0].GetProperty("text").GetString()!;

    private static string LastUserText(RecordedRequest request) =>
        request.Json.GetProperty("messages")[0].GetProperty("content").EnumerateArray().Last().GetProperty("text").GetString()!;

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition did not hold within 30 seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    // A new person submits the log, and the queue is processed. The caller disposes the database.
    private async Task<(Guid RunId, RunsDatabase Database)> SubmitAndProcessAsync(string log, CancellationToken cancellationToken)
    {
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(log, fileName: null, cancellationToken);
        await factory.ProcessQueuedRunsAsync(cancellationToken);
        return (runId, new RunsDatabase(factory.ConnectionString));
    }
}
