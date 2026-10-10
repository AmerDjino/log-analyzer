using System.Net;
using System.Text.Json;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class ListRunsTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    private static readonly StoredFinding PaymentFailed = new(
        "Payment for order 42 failed",
        "error",
        "dependency",
        [1],
        "Payment for order 42 failed",
        "The payment call failed, so the order was not paid.",
        "Check the payment service's availability.");

    [Fact]
    [Trait("Scenario", "analysis-history/runs-are-listed-newest-first")]
    public async Task List_ReturnsNewestFirst()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        await using var database = new RunsDatabase(factory.ConnectionString);
        var submitted = new List<(Guid Id, DateTimeOffset At)>();
        for (var hour = 0; hour < 3; hour++)
        {
            if (hour > 0)
            {
                factory.Clock.Advance(TimeSpan.FromHours(1));
            }

            var at = factory.Clock.GetUtcNow();
            submitted.Add((await runs.SubmitAcceptedAsync($"fail: Payment for order {hour} failed", $"run-{hour}.log", cancellationToken), at));
        }

        // The middle run has completed, so its item shows a highest severity.
        await database.CompleteRunAsync(
            submitted[1].Id, submitted[1].At, submitted[1].At, "One payment failed.", "error", [PaymentFailed], cancellationToken);

        var page = await runs.ListPageAsync(cancellationToken);

        Assert.Equal([submitted[2].Id, submitted[1].Id, submitted[0].Id], RunsClient.IdsOf(page));
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            [submitted[2].At, submitted[1].At, submitted[0].At],
            items.Select(item => item.GetProperty("submittedAt").GetDateTimeOffset()));
        Assert.Equal("run-1.log", items[1].GetProperty("fileName").GetString());
        Assert.Equal("completed", items[1].GetProperty("status").GetString());
        Assert.Equal("error", items[1].GetProperty("highestSeverity").GetString());
        Assert.Equal("queued", items[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("highestSeverity").ValueKind);
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/runs-are-paged")]
    public async Task List_ReturnsTwentyPerPageWithNextCursor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var submitted = new List<Guid>();
        for (var index = 0; index < 25; index++)
        {
            factory.Clock.Advance(TimeSpan.FromMinutes(1));
            submitted.Add(await runs.SubmitAcceptedAsync($"info: request {index} handled", fileName: null, cancellationToken));
        }

        var newestFirst = Enumerable.Reverse(submitted).ToList();

        var firstPage = await runs.ListPageAsync(cancellationToken);
        var nextCursor = firstPage.GetProperty("nextCursor").GetString();
        var secondPage = await runs.ListPageAsync(cancellationToken, cursor: nextCursor);

        Assert.Equal(newestFirst[..20], RunsClient.IdsOf(firstPage));
        Assert.False(string.IsNullOrEmpty(nextCursor));
        Assert.Equal(newestFirst[20..], RunsClient.IdsOf(secondPage));
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextCursor").ValueKind);
    }

    // Two runs submitted at the same time are still listed once each, ordered by id.
    [Fact]
    public async Task List_PagesThroughRunsSubmittedAtTheSameTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var submitted = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            submitted.Add(await runs.SubmitAcceptedAsync($"info: request {index} handled", fileName: null, cancellationToken));
        }

        var listed = new List<Guid>();
        string? cursor = null;
        do
        {
            var page = await runs.ListPageAsync(cancellationToken, limit: "2", cursor: cursor);
            listed.AddRange(RunsClient.IdsOf(page));
            cursor = page.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        Assert.Equal(submitted.Order(), listed.Order());
        Assert.Equal(5, listed.Count);
    }

    [Fact]
    [Trait("Scenario", "analysis-history/other-people-s-runs-are-not-listed")]
    public async Task List_OmitsOtherPeoplesRuns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anasRuns = new[]
        {
            await ana.SubmitAcceptedAsync("info: Ana's first run", fileName: null, cancellationToken),
            await ana.SubmitAcceptedAsync("info: Ana's second run", fileName: null, cancellationToken),
        };
        var bobsRun = await bob.SubmitAcceptedAsync("info: Bob's run", fileName: null, cancellationToken);

        var anasPage = await ana.ListPageAsync(cancellationToken);
        var bobsPage = await bob.ListPageAsync(cancellationToken);

        Assert.Equal(anasRuns.Order(), RunsClient.IdsOf(anasPage).Order());
        Assert.Equal([bobsRun], RunsClient.IdsOf(bobsPage));
    }

    [Fact]
    [Trait("Scenario", "analysis-history/invalid-page-cursor-is-rejected")]
    public async Task List_RejectsUnreadableCursor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);

        using var response = await runs.ListAsync(cancellationToken, cursor: "not-a-cursor");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "INVALID_CURSOR", cancellationToken);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("twenty")]
    [InlineData("1.5")]
    public async Task List_RejectsLimitOutsideOneToHundred(string limit)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);

        using var response = await runs.ListAsync(cancellationToken, limit: limit);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "INVALID_LIMIT", cancellationToken);
    }

    [Fact]
    public async Task List_AcceptsLimitsFromOneToHundred()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var older = await runs.SubmitAcceptedAsync("info: older run", fileName: null, cancellationToken);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await runs.SubmitAcceptedAsync("info: newer run", fileName: null, cancellationToken);

        var one = await runs.ListPageAsync(cancellationToken, limit: "1");
        var hundred = await runs.ListPageAsync(cancellationToken, limit: "100");

        Assert.Equal([newer], RunsClient.IdsOf(one));
        Assert.Equal(JsonValueKind.String, one.GetProperty("nextCursor").ValueKind);
        Assert.Equal([newer, older], RunsClient.IdsOf(hundred));
    }
}
