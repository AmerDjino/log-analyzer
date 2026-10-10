using System.Net;
using System.Text;
using System.Text.Json;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class ReadRunTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    private const string Log =
        "2026-10-10T09:00:01Z info: Orders.Api[0] Application started\n"
        + "2026-10-10T09:00:04Z fail: Orders.Api[0] Payment for order 42 failed\n"
        + "2026-10-10T09:00:05Z crit: Orders.Api[0] Database connection pool exhausted\n";

    private static readonly StoredFinding PoolExhausted = new(
        "Database connection pool exhausted",
        "critical",
        "resource",
        [3],
        "Database connection pool exhausted",
        "Requests wait for a connection until they time out.",
        "Check for connections that are not returned to the pool.");

    private static readonly StoredFinding PaymentFailed = new(
        "Payment for order 42 failed",
        "error",
        "dependency",
        [2],
        "Payment for order 42 failed",
        "The payment call failed, so the order was not paid.",
        "Check the payment service's availability.");

    [Fact]
    [Trait("Scenario", "analysis-history/completed-run-is-returned-with-its-findings")]
    public async Task Read_ReturnsCompletedRunWithFindings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var submittedAt = factory.Clock.GetUtcNow();
        var runId = await runs.SubmitAcceptedAsync(Log, "orders-api.log", cancellationToken);
        await database.CompleteRunAsync(
            runId,
            submittedAt.AddSeconds(1),
            submittedAt.AddSeconds(3),
            "The payment service ran out of database connections.",
            "critical",
            [PoolExhausted, PaymentFailed],
            cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);

        Assert.Equal(runId, run.GetProperty("id").GetGuid());
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal("orders-api.log", run.GetProperty("fileName").GetString());
        Assert.Equal(submittedAt, run.GetProperty("submittedAt").GetDateTimeOffset());
        Assert.Equal(submittedAt.AddSeconds(1), run.GetProperty("startedAt").GetDateTimeOffset());
        Assert.Equal(submittedAt.AddSeconds(3), run.GetProperty("finishedAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("failureReason").ValueKind);

        var statistics = run.GetProperty("statistics");
        Assert.Equal(3, statistics.GetProperty("lineCount").GetInt32());
        Assert.Equal(Encoding.UTF8.GetByteCount(Log), statistics.GetProperty("byteCount").GetInt32());
        var levels = statistics.GetProperty("levels");
        Assert.Equal(
            new Dictionary<string, int> { ["critical"] = 1, ["error"] = 1, ["warning"] = 0, ["info"] = 1, ["debug"] = 0, ["trace"] = 0 },
            levels.EnumerateObject().ToDictionary(level => level.Name, level => level.Value.GetInt32()));
        var timeRange = statistics.GetProperty("timeRange");
        Assert.Equal("2026-10-10T09:00:01Z", timeRange.GetProperty("start").GetString());
        Assert.Equal("2026-10-10T09:00:05Z", timeRange.GetProperty("end").GetString());
        Assert.True(timeRange.GetProperty("utc").GetBoolean());

        Assert.Equal("The payment service ran out of database connections.", run.GetProperty("summary").GetString());
        var findings = run.GetProperty("findings").EnumerateArray().ToList();
        Assert.Equal(2, findings.Count);
        AssertFinding(PoolExhausted, findings[0]);
        AssertFinding(PaymentFailed, findings[1]);
    }

    // A run that has not completed has neither a summary nor findings, but has its statistics.
    [Fact]
    public async Task Read_ReturnsQueuedRunWithStatisticsAndWithoutFindings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        var run = await runs.ReadRunAsync(runId, cancellationToken);

        Assert.Equal("queued", run.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("fileName").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("startedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("finishedAt").ValueKind);
        Assert.Equal(3, run.GetProperty("statistics").GetProperty("lineCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("summary").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("findings").ValueKind);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/unknown-run-id")]
    public async Task Read_AnswersUnknownIdWithNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);

        using var shortId = await runs.ReadAsync("7f3c", cancellationToken);
        using var unknownId = await runs.ReadAsync(Guid.CreateVersion7().ToString(), cancellationToken);

        await ProblemAssert.HasCodeAsync(shortId, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        await ProblemAssert.HasCodeAsync(unknownId, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/another-person-s-run-is-not-found")]
    public async Task Read_AnswersAnotherPersonsRunWithNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anasRun = await ana.SubmitAcceptedAsync(Log, "orders-api.log", cancellationToken);

        using var anotherPersonsRun = await bob.ReadAsync(anasRun.ToString(), cancellationToken);
        using var unknownRun = await bob.ReadAsync(Guid.CreateVersion7().ToString(), cancellationToken);

        var expected = await ProblemAssert.HasCodeAsync(unknownRun, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        var actual = await ProblemAssert.HasCodeAsync(
            anotherPersonsRun, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        AssertSameProblem(expected, actual);
    }

    // The id is read as text, so a malformed one gets the run endpoints' 404 rather than routing's, which has no code.
    [Fact]
    public async Task Read_AnswersMalformedIdLikeAnotherPersonsRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anasRun = await ana.SubmitAcceptedAsync(Log, "orders-api.log", cancellationToken);

        using var anotherPersonsRun = await bob.ReadAsync(anasRun.ToString(), cancellationToken);
        using var malformedId = await bob.ReadAsync("7f3c", cancellationToken);

        var expected = await ProblemAssert.HasCodeAsync(
            anotherPersonsRun, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        var actual = await ProblemAssert.HasCodeAsync(malformedId, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        AssertSameProblem(expected, actual);
    }

    private static void AssertFinding(StoredFinding expected, JsonElement actual)
    {
        Assert.Equal(expected.Title, actual.GetProperty("title").GetString());
        Assert.Equal(expected.Severity, actual.GetProperty("severity").GetString());
        Assert.Equal(expected.Category, actual.GetProperty("category").GetString());
        Assert.Equal(expected.LineRefs, actual.GetProperty("lineRefs").EnumerateArray().Select(line => line.GetInt32()));
        Assert.Equal(expected.Evidence, actual.GetProperty("evidence").GetString());
        Assert.Equal(expected.Explanation, actual.GetProperty("explanation").GetString());
        Assert.Equal(expected.SuggestedAction, actual.GetProperty("suggestedAction").GetString());
    }

    private static void AssertSameProblem(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.GetProperty("title").GetString(), actual.GetProperty("title").GetString());
        Assert.Equal(expected.GetProperty("detail").GetString(), actual.GetProperty("detail").GetString());
    }
}
