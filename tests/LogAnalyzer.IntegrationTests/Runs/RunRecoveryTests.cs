using System.Text.Json;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// Runs across a restart. The class's API has no worker, so runs stay as the test leaves them; a second API host with
/// the real worker then starts against the same database, as the API does after a restart.
/// </summary>
public sealed class RunRecoveryTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "analysis-history/interrupted-run-fails-after-a-restart")]
    public async Task Restart_FailsRunLeftRunning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        await database.SetRunningAsync(runId, factory.Clock.GetUtcNow(), cancellationToken);

        await using var restarted = new RestartedApiFactory(factory.ConnectionString);
        var run = await (await SignInAsync(restarted, person, cancellationToken)).ReadWhenFinishedAsync(runId, cancellationToken);

        Assert.Equal("failed", run.GetProperty("status").GetString());
        Assert.Equal(FailureReasons.Interrupted, run.GetProperty("failureReason").GetString());
        Assert.Equal(JsonValueKind.String, run.GetProperty("finishedAt").ValueKind);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/queued-run-is-processed-after-a-restart")]
    public async Task Restart_ProcessesQueuedRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);

        await using var restarted = new RestartedApiFactory(factory.ConnectionString);
        var run = await (await SignInAsync(restarted, person, cancellationToken)).ReadWhenFinishedAsync(runId, cancellationToken);

        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.NotEmpty(run.GetProperty("findings").EnumerateArray());
    }

    // At a graceful shutdown the model call is cancelled and the run fails at once, without waiting for the next start.
    [Fact]
    public async Task Shutdown_FailsRunInProgressAsInterrupted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        var model = new BlockingAnalysisModel();
        var restarted = new RestartedApiFactory(factory.ConnectionString, model);

        try
        {
            // Starts the API, whose worker claims the run and calls the model.
            _ = restarted.Services;
            await model.Called.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            Assert.Equal("running", (await database.ReadRunAsync(runId, cancellationToken)).Status);
        }
        finally
        {
            await restarted.DisposeAsync();
        }

        var run = await database.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("failed", run.Status);
        Assert.Equal(FailureReasons.Interrupted, run.FailureReason);
    }

    // A session from the first API is not carried over: the person signs in to the restarted one.
    private static async Task<RunsClient> SignInAsync(
        RestartedApiFactory restarted, AccountResponse person, CancellationToken cancellationToken)
    {
        var accounts = new AccountsClient(restarted.CreateClient());
        using var response = await accounts.SignInAsync(person.Email, AccountsClient.Password, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new RunsClient(accounts.Http);
    }
}
