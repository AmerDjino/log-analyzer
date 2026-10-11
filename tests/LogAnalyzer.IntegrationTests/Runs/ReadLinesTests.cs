using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

public sealed class ReadLinesTests(RunsApiFactory factory) : IClassFixture<RunsApiFactory>
{
    private const int LineCount = 500;

    // Line N reads "line N of 500", so a line returned with the wrong number shows. Lines are stored at submission, so
    // the run needs no processing.
    private static readonly string Log =
        string.Concat(Enumerable.Range(1, LineCount).Select(number => $"{LineText(number)}\n"));

    [Fact]
    [Trait("Scenario", "analysis-history/line-range-is-returned-with-numbers")]
    public async Task ReadLines_ReturnsRangeWithLineNumbers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var response = await runs.ReadLinesAsync(runId.ToString(), "117", "123", cancellationToken);

        Assert.Equal(Numbered(117, 123), await LinesOfAsync(response, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "analysis-history/range-past-the-end-is-clamped")]
    public async Task ReadLines_ClampsRangePastTheEnd()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var response = await runs.ReadLinesAsync(runId.ToString(), "495", "520", cancellationToken);

        Assert.Equal(Numbered(495, 500), await LinesOfAsync(response, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "analysis-history/another-person-s-log-lines-are-not-found")]
    public async Task ReadLines_AnswersAnotherPersonsRunWithNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anasRun = await ana.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var response = await bob.ReadLinesAsync(anasRun.ToString(), "1", "10", cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
    }

    // The id is read as text, as for GET /runs/{id}, so a malformed one gets the run endpoints' 404 rather than routing's,
    // and the answer reveals nothing about other people's runs.
    [Fact]
    public async Task ReadLines_AnswersMalformedAndUnknownIdsLikeAnotherPersonsRun()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ana, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var (bob, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var anasRun = await ana.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var anotherPersonsRun = await bob.ReadLinesAsync(anasRun.ToString(), "1", "10", cancellationToken);
        using var malformedId = await bob.ReadLinesAsync("7f3c", "1", "10", cancellationToken);
        using var unknownId = await bob.ReadLinesAsync(Guid.CreateVersion7().ToString(), "1", "10", cancellationToken);

        var expected = await ProblemAssert.HasCodeAsync(
            anotherPersonsRun, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        var malformed = await ProblemAssert.HasCodeAsync(
            malformedId, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        var unknown = await ProblemAssert.HasCodeAsync(
            unknownId, HttpStatusCode.NotFound, "RUN_NOT_FOUND", cancellationToken);
        AssertSameProblem(expected, malformed);
        AssertSameProblem(expected, unknown);
    }

    [Fact]
    public async Task ReadLines_StartsAtLineOneWhenFromIsBelowOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var fromZero = await runs.ReadLinesAsync(runId.ToString(), "0", "3", cancellationToken);
        using var fromNegative = await runs.ReadLinesAsync(runId.ToString(), "-5", "3", cancellationToken);

        Assert.Equal(Numbered(1, 3), await LinesOfAsync(fromZero, cancellationToken));
        Assert.Equal(Numbered(1, 3), await LinesOfAsync(fromNegative, cancellationToken));
    }

    [Fact]
    public async Task ReadLines_ReturnsEmptyListForReversedRangeOrRangeAfterLastLine()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var reversed = await runs.ReadLinesAsync(runId.ToString(), "123", "117", cancellationToken);
        using var afterLastLine = await runs.ReadLinesAsync(runId.ToString(), "501", "510", cancellationToken);

        Assert.Empty(await LinesOfAsync(reversed, cancellationToken));
        Assert.Empty(await LinesOfAsync(afterLastLine, cancellationToken));
    }

    // The spec names no error for a value the API cannot read, so the framework answers, without a code.
    [Theory]
    [InlineData(null, "10")]
    [InlineData("1", null)]
    [InlineData("one", "10")]
    [InlineData("1", "ten")]
    public async Task ReadLines_RejectsMissingOrNonNumericFromOrTo(string? from, string? to)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);
        var runId = await runs.SubmitAcceptedAsync(Log, fileName: null, cancellationToken);

        using var response = await runs.ReadLinesAsync(runId.ToString(), from, to, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.False(problem.TryGetProperty("code", out _), $"A code in: {problem}");
    }

    private static string LineText(int number) => $"line {number} of {LineCount}";

    // Lines first to last of the log, with their numbers.
    private static List<(int Number, string Text)> Numbered(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(number => (number, LineText(number))).ToList();

    // The lines of an answer that must be 200, in the order returned.
    private static async Task<List<(int Number, string Text)>> LinesOfAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("lines").EnumerateArray()
            .Select(line => (line.GetProperty("number").GetInt32(), line.GetProperty("text").GetString()!))
            .ToList();
    }

    private static void AssertSameProblem(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.GetProperty("title").GetString(), actual.GetProperty("title").GetString());
        Assert.Equal(expected.GetProperty("detail").GetString(), actual.GetProperty("detail").GetString());
    }
}
