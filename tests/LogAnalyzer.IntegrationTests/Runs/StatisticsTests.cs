using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class StatisticsTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "log-ingestion/level-counts-come-from-the-log")]
    public async Task Statistics_CountLevelsFromTheLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.Levels10), "levels.log", cancellationToken);
        // The model's text claims other numbers; the statistics never come from it.
        factory.Model.Reply(
            runId,
            ModelReplies.Analysis(
                "The log shows 7 errors and no warnings.",
                ModelReplies.Finding(
                    "Database connection pool exhausted", "critical", "resource", [4], "Database connection pool exhausted")));

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var statistics = run.GetProperty("statistics");
        Assert.Equal(10, statistics.GetProperty("lineCount").GetInt32());
        Assert.Equal(
            new Dictionary<string, int> { ["critical"] = 0, ["error"] = 3, ["warning"] = 2, ["info"] = 4, ["debug"] = 0, ["trace"] = 0 },
            statistics.GetProperty("levels").EnumerateObject().ToDictionary(level => level.Name, level => level.Value.GetInt32()));
    }

    [Fact]
    [Trait("Scenario", "log-ingestion/log-without-recognizable-levels-or-timestamps")]
    public async Task Statistics_ReportNoLevelsAndNoTimeRange()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.NoLevels), "export.log", cancellationToken);
        factory.Model.Reply(runId, ModelReplies.Analysis("The nightly export finished without problems."));

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        var statistics = run.GetProperty("statistics");
        Assert.Equal(4, statistics.GetProperty("lineCount").GetInt32());
        Assert.All(statistics.GetProperty("levels").EnumerateObject(), level => Assert.Equal(0, level.Value.GetInt32()));
        Assert.Equal(6, statistics.GetProperty("levels").EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, statistics.GetProperty("timeRange").ValueKind);
        Assert.Empty(run.GetProperty("findings").EnumerateArray());
    }

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
