namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>The real worker, with the stand-in model, completes runs without help from the test.</summary>
public sealed class RunWorkerTests(WorkerApiFactory factory) : IClassFixture<WorkerApiFactory>
{
    // The test clock stands still, so the worker can only have been woken by the signal the submission raised.
    [Fact]
    public async Task Worker_ProcessesSubmittedRunWhenSignalled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);

        var run = await runs.ReadWhenFinishedAsync(runId, cancellationToken);

        Assert.Equal("completed", run.GetProperty("status").GetString());
        // The stand-in flags the first three error lines of orders-api-40.log, each by its own line.
        Assert.Equal(
            [12, 13, 15],
            run.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("lineRefs")[0].GetInt32()));
    }

    // A run queued without the signal, as if by another process, is found when the worker polls.
    [Fact]
    public async Task Worker_FindsUnsignalledRunWhenItPolls()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, person) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        string[] lines = ["info: Orders.Api[0] Application started", "fail: Orders.Api[0] Order 1003 could not be stored"];
        var runId = await database.InsertQueuedRunAsync(person.Id, lines, factory.Clock.GetUtcNow(), cancellationToken);

        // Moved on every read, so the poll fires whenever the worker started waiting.
        var run = await runs.ReadWhenFinishedAsync(
            runId, cancellationToken, beforeEachRead: () => factory.Clock.Advance(WorkerApiFactory.PollInterval));

        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal(2, run.GetProperty("findings")[0].GetProperty("lineRefs")[0].GetInt32());
    }
}
