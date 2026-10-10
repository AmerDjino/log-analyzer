using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class AttemptTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    [Fact]
    [Trait("Scenario", "analysis-history/rejected-then-accepted-attempts-are-both-recorded")]
    public async Task Process_RecordsRejectedThenAcceptedAttempts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        // A marker in the rejected response, to show that none of its text is stored.
        const string Marker = "rejected-response-marker-5f2b";
        factory.Model.Reply(runId, ModelReplies.CitesLinePastTheEnd($"The database ran out of connections. {Marker}"));
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);
        var start = factory.Clock.GetUtcNow();

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        Assert.Equal("completed", (await runs.ReadRunAsync(runId, cancellationToken)).GetProperty("status").GetString());
        var attempts = await database.ReadAttemptsAsync(runId, cancellationToken);
        Assert.Equal(2, attempts.Count);
        var rejected = attempts[0];
        Assert.Equal(1, rejected.Sequence);
        Assert.Equal("rejected", rejected.Outcome);
        Assert.Equal([(ReasonCodes.LineOutOfRange, "findings[0].line_refs[0]")], rejected.RejectionReasons);
        var accepted = attempts[1];
        Assert.Equal(2, accepted.Sequence);
        Assert.Equal("accepted", accepted.Outcome);
        Assert.Empty(accepted.RejectionReasons);
        Assert.All(attempts, attempt =>
        {
            Assert.Equal(ScriptedAnalysisModel.Model, attempt.Model);
            Assert.Equal(ScriptedAnalysisModel.PromptVersion, attempt.PromptVersion);
        });
        // Each scripted call takes one second on the test clock, and the corrected request follows the rejection at once.
        Assert.True(rejected.StartedAt >= start, $"Started at {rejected.StartedAt:O}, before processing at {start:O}.");
        Assert.Equal(rejected.StartedAt.AddSeconds(1), rejected.FinishedAt);
        Assert.Equal(rejected.FinishedAt, accepted.StartedAt);
        Assert.Equal(accepted.StartedAt.AddSeconds(1), accepted.FinishedAt);
        Assert.DoesNotContain(
            await database.ReadAttemptValuesAsync(runId, cancellationToken),
            value => value.Contains(Marker, StringComparison.Ordinal));
    }

    // A field name the model made up is part of its response, so its path is stored with the name masked.
    [Fact]
    public async Task Process_StoresRejectionPathsWithoutNamesTheModelMadeUp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var runId = await runs.SubmitAcceptedAsync(FixtureLogs.Read(FixtureLogs.OrdersApi40), "orders-api.log", cancellationToken);
        factory.Model.Reply(
            runId,
            """
            {"summary":"The database ran out of connections.","findings":[{"title":"Database connection pool exhausted",
            "severity":"critical","category":"resource","line_refs":[12],"evidence":"Database connection pool exhausted",
            "explanation":"Requests wait for a connection.","suggested_action":"Check the pool size.","max tokens":"secret-name-9c1d"}]}
            """);
        factory.Model.Reply(runId, ModelReplies.OrdersAnalysis);

        await factory.ProcessQueuedRunsAsync(cancellationToken);

        var rejected = (await database.ReadAttemptsAsync(runId, cancellationToken))[0];
        Assert.Equal([(ReasonCodes.SchemaViolation, "findings[0].*")], rejected.RejectionReasons);
        Assert.DoesNotContain(
            await database.ReadAttemptValuesAsync(runId, cancellationToken),
            value => value.Contains("max tokens", StringComparison.Ordinal));
    }
}
