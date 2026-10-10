using System.Text.Json;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;
using LogAnalyzer.UnitTests.Validation;

namespace LogAnalyzer.UnitTests.Processing;

/// <summary>The stand-in model: deterministic findings that cite real lines and pass the validator.</summary>
public sealed class StandInAnalysisModelTests
{
    public static TheoryData<string> FixtureLogs =>
        [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "logs")).Select(path => Path.GetFileName(path)).Order()];

    [Theory]
    [MemberData(nameof(FixtureLogs))]
    public async Task Analyze_RepliesWithAnAnalysisTheValidatorAccepts(string log)
    {
        var lines = Fixtures.LogLines(log);

        var reply = await AnalyzeAsync(lines);

        Assert.Equal(StopReason.Completed, reply.StopReason);
        Assert.Equal("stand-in", reply.Model);
        Assert.Equal("stand-in", reply.PromptVersion);
        ValidationAssert.Accepted(ResponseValidator.Validate(new ValidationInput(reply.Text, reply.StopReason, lines)));
    }

    [Fact]
    public async Task Analyze_FlagsUpToThreeLinesCriticalThenErrorThenWarning()
    {
        string[] lines =
        [
            "warn: Orders.Api[0] Connection pool at 90% of its limit",
            "fail: Orders.Api[0] Order 1003 could not be stored",
            "info: Orders.Api[0] Order 1004 stored",
            "crit: Orders.Api[0] Database connection pool exhausted",
            "fail: Orders.Api[0] Call to payments-api failed with status 503",
            "fail: Orders.Api[0] Order 1005 could not be stored",
        ];

        var analysis = Accepted(lines, await AnalyzeAsync(lines));

        Assert.Equal(
            [
                ("Critical logged on line 4", Severity.Critical, 4),
                ("Error logged on line 2", Severity.Error, 2),
                ("Error logged on line 5", Severity.Error, 5),
            ],
            analysis.Findings.Select(finding => (finding.Title, finding.Severity, Assert.Single(finding.LineRefs))));
    }

    [Fact]
    public async Task Analyze_FlagsALineOnlyOnceWhenItsTextRepeats()
    {
        string[] lines =
        [
            "fail: Orders.Api[0] Order could not be stored",
            "  fail: Orders.Api[0] Order could not be stored  ",
            "warn: Orders.Api[0] Connection pool at 90% of its limit",
        ];

        var analysis = Accepted(lines, await AnalyzeAsync(lines));

        Assert.Equal([1, 3], analysis.Findings.Select(finding => Assert.Single(finding.LineRefs)));
    }

    [Theory]
    [InlineData("fail: Orders.Api[0] Unhandled exception in checkout", FindingCategory.Exception)]
    [InlineData("fail: Orders.Api[0] System.NullReferenceException: Object reference not set", FindingCategory.Exception)]
    [InlineData("fail: Orders.Api[0] Request to inventory-service timed out after 30 s", FindingCategory.Timeout)]
    [InlineData("fail: Orders.Api[0] Timeout while waiting for a connection", FindingCategory.Timeout)]
    [InlineData("fail: Orders.Api[0] Call to payments-api failed with status 503", FindingCategory.Other)]
    public async Task Analyze_ChoosesTheCategoryByKeyword(string line, FindingCategory expected)
    {
        var analysis = Accepted([line], await AnalyzeAsync([line]));

        Assert.Equal(expected, Assert.Single(analysis.Findings).Category);
    }

    [Fact]
    public async Task Analyze_QuotesTheTrimmedLineAsEvidence()
    {
        string[] lines = ["\t  [12:00:01 ERR] Payment for order 42 failed   "];

        var analysis = Accepted(lines, await AnalyzeAsync(lines));

        Assert.Equal("[12:00:01 ERR] Payment for order 42 failed", Assert.Single(analysis.Findings).Evidence);
    }

    // A request may carry only some of a log's lines, with their original numbers.
    [Fact]
    public async Task Analyze_CitesTheLinesOriginalNumbers()
    {
        var request = new AnalysisRequest(
            Guid.CreateVersion7(),
            [new(41, "info: Orders.Api[0] Order 1015 stored"), new(57, "fail: Orders.Api[0] Order 1016 could not be stored")],
            Correction: null);

        var reply = await new StandInAnalysisModel().AnalyzeAsync(request, CancellationToken.None);

        using var response = JsonDocument.Parse(reply.Text);
        var finding = Assert.Single(response.RootElement.GetProperty("findings").EnumerateArray());
        Assert.Equal(57, Assert.Single(finding.GetProperty("line_refs").EnumerateArray()).GetInt32());
        Assert.Equal("Error logged on line 57", finding.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Analyze_SaysThatNoAiModelAnalyzedTheLog()
    {
        string[] lines = ["fail: Orders.Api[0] Order 1003 could not be stored"];

        var analysis = Accepted(lines, await AnalyzeAsync(lines));

        Assert.Contains("No AI model", analysis.Summary, StringComparison.Ordinal);
        var finding = Assert.Single(analysis.Findings);
        Assert.Contains("No AI model", finding.Explanation, StringComparison.Ordinal);
        Assert.Contains("stand-in", finding.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_ReportsNoFindingsForALogWithoutCriticalErrorOrWarningLines()
    {
        string[] lines = ["info: Orders.Api[0] Application started", "   at Orders.Api.Program.Main()", "dbug: Cache hit"];

        var analysis = Accepted(lines, await AnalyzeAsync(lines));

        Assert.Empty(analysis.Findings);
        Assert.Contains("no critical, error or warning lines", analysis.Summary, StringComparison.Ordinal);
    }

    private static Task<ModelReply> AnalyzeAsync(IReadOnlyList<string> lines) =>
        new StandInAnalysisModel().AnalyzeAsync(
            new AnalysisRequest(Guid.CreateVersion7(), [.. lines.Select((text, index) => new NumberedLine(index + 1, text))], null),
            CancellationToken.None);

    private static Analysis Accepted(IReadOnlyList<string> lines, ModelReply reply) =>
        ValidationAssert.Accepted(ResponseValidator.Validate(new ValidationInput(reply.Text, reply.StopReason, lines)));
}
