namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class StatisticsTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "log-ingestion/time-range-spans-out-of-order-lines")]
    public async Task Statistics_SpanOutOfOrderTimestamps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(
            "2026-10-10T09:00:05Z info: Orders.Api[0] Second event\n2026-10-10T09:00:01Z info: Orders.Api[0] First event\n",
            fileName: null,
            cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);

        var timeRange = run.GetProperty("statistics").GetProperty("timeRange");
        Assert.Equal("2026-10-10T09:00:01Z", timeRange.GetProperty("start").GetString());
        Assert.Equal("2026-10-10T09:00:05Z", timeRange.GetProperty("end").GetString());
        Assert.True(timeRange.GetProperty("utc").GetBoolean());
    }

    // Times without an offset are returned as the log wrote them, without one.
    [Fact]
    public async Task Statistics_ReturnTimesWithoutOffsetAsWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(
            "2026-10-10 09:00:01,250 ERROR [main] Payment failed\n2026-10-10 09:00:03,500 INFO [main] Retried\n",
            fileName: null,
            cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);

        var timeRange = run.GetProperty("statistics").GetProperty("timeRange");
        Assert.Equal("2026-10-10T09:00:01.25", timeRange.GetProperty("start").GetString());
        Assert.Equal("2026-10-10T09:00:03.5", timeRange.GetProperty("end").GetString());
        Assert.False(timeRange.GetProperty("utc").GetBoolean());
    }
}
