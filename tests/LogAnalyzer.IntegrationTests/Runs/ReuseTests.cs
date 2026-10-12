using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LogAnalyzer.Core.Processing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// An identical resubmission of the person's own completed run, analyzed with the prompt version and model configured
/// now, answers with that run. Each test sets the scripted model's configuration before it submits.
/// </summary>
public sealed class ReuseTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    private const string PromptV1 = "log-analysis/v1";

    private const string PromptV2 = "log-analysis/v2";

    private const string TestModel = "claude-test";

    private static readonly string Log = FixtureLogs.Read(FixtureLogs.OrdersApi40);

    [Fact]
    [Trait("Scenario", "analysis-history/identical-resubmission-reuses-the-run")]
    public async Task Submit_ReusesOwnCompletedRunForIdenticalContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (ana, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var completed = await CompleteRunAsync(ana, cancellationToken);
        var calls = factory.Model.Requests.Count;

        using var response = await ana.PasteAsync(Log, cancellationToken);
        await factory.ProcessQueuedRunsAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(completed, (await response.Content.ReadFromJsonAsync<SubmissionResponse>(cancellationToken))!.Id);
        Assert.Equal(1, await database.CountRunsAsync(person.Id, cancellationToken));
        Assert.Equal(calls, factory.Model.Requests.Count);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/new-prompt-version-analyzes-again")]
    public async Task Submit_AnalyzesAgainWithNewPromptVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var completed = await CompleteRunAsync(runs, cancellationToken);

        Configure(PromptV2, TestModel);
        var again = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        factory.Model.Reply(again, ModelReplies.OrdersAnalysis);
        await factory.ProcessQueuedRunsAsync(cancellationToken);

        Assert.NotEqual(completed, again);
        Assert.Equal(2, await database.CountRunsAsync(person.Id, cancellationToken));
        Assert.Single(factory.Model.RequestsFor(again));
    }

    [Fact]
    [Trait("Scenario", "analysis-history/another-person-s-identical-log-is-analyzed-again")]
    public async Task Submit_AnalyzesAnotherPersonsIdenticalLogAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anas = await CompleteRunAsync(ana, cancellationToken);

        using var response = await bob.PasteAsync(Log, cancellationToken);
        var bobs = await RunsClient.AcceptedRunIdAsync(response, cancellationToken);
        factory.Model.Reply(bobs, ModelReplies.OrdersAnalysis);
        await factory.ProcessQueuedRunsAsync(cancellationToken);

        Assert.NotEqual(anas, bobs);
        Assert.Single(factory.Model.RequestsFor(bobs));
    }

    [Fact]
    public async Task Submit_AnswersReuseWithTheRunsIdAndCompletedStatusOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var completed = await CompleteRunAsync(runs, cancellationToken);

        using var response = await runs.PasteAsync(Log, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(["id", "status"], body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(completed, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("completed", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Submit_DoesNotReuseAQueuedRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var queued = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        var again = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        Assert.NotEqual(queued, again);
    }

    [Fact]
    public async Task Submit_DoesNotReuseAFailedRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var failed = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);
        factory.Model.Fail(failed, FailureReasons.ModelUnavailable);
        await factory.ProcessQueuedRunsAsync(cancellationToken);
        Assert.Equal("failed", (await runs.ReadRunAsync(failed, cancellationToken)).GetProperty("status").GetString());

        var again = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        Assert.NotEqual(failed, again);
    }

    [Fact]
    public async Task Submit_AnalyzesAgainWithAnotherModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var completed = await CompleteRunAsync(runs, cancellationToken);

        Configure(PromptV1, "claude-other");
        var again = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        Assert.NotEqual(completed, again);
    }

    // The file name is not part of the content: the earlier run is returned, and keeps its own name.
    [Fact]
    public async Task Submit_ReusesTheRunWhateverTheFileName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Configure(PromptV1, TestModel);
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var completed = await CompleteRunAsync(runs, cancellationToken, fileName: "orders-api.log");

        using var response = await runs.UploadAsync("renamed.log", Encoding.UTF8.GetBytes(Log), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(completed, (await response.Content.ReadFromJsonAsync<SubmissionResponse>(cancellationToken))!.Id);
        Assert.Equal("orders-api.log", (await runs.ReadRunAsync(completed, cancellationToken)).GetProperty("fileName").GetString());
    }

    private void Configure(string promptVersion, string model)
    {
        factory.Model.ConfiguredPromptVersion = promptVersion;
        factory.Model.ConfiguredModel = model;
    }

    // Submits the log, as an upload when it has a file name, and completes its run with an accepted analysis.
    private async Task<Guid> CompleteRunAsync(RunsClient runs, CancellationToken cancellationToken, string? fileName = null)
    {
        var runId = await runs.SubmitAcceptedAsync(Log, fileName, cancellationToken);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);
        await factory.ProcessQueuedRunsAsync(cancellationToken);
        Assert.Equal("completed", (await runs.ReadRunAsync(runId, cancellationToken)).GetProperty("status").GetString());
        return runId;
    }
}
