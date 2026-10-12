using LogAnalyzer.IntegrationTests.Runs;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>Without model settings, the API analyzes runs with the stand-in and needs no key.</summary>
public sealed class ModelProviderTests(WorkerApiFactory factory) : IClassFixture<WorkerApiFactory>
{
    [Fact]
    [Trait("Scenario", "model-gateway/stand-in-runs-without-a-key")]
    public async Task StandIn_CompletesRunWithoutKey()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);

        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), fileName: null, cancellationToken);
        var run = await runs.ReadWhenFinishedAsync(runId, cancellationToken);

        Assert.Equal("completed", run.GetProperty("status").GetString());
        var attempt = Assert.Single(await database.ReadAttemptsAsync(runId, cancellationToken));
        Assert.Equal("stand-in", attempt.Model);
        Assert.Equal("stand-in", attempt.PromptVersion);
    }
}
