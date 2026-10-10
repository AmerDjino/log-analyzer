using System.Text.Json.Nodes;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.UnitTests.Validation;

/// <summary>The response-validation scenarios: when a model response is rejected, and with which reasons.</summary>
public sealed class ResponseValidationTests
{
    [Fact]
    [Trait("Scenario", "response-validation/truncated-response-is-rejected")]
    public void Validate_RejectsTruncatedResponse()
    {
        var result = ResponseValidator.Validate(
            Fixtures.Input("response-validation/truncated-response-is-rejected.txt", stopReason: StopReason.OutputLimit));

        ValidationAssert.Rejected(result, ("TRUNCATED", ""));
    }

    [Fact]
    [Trait("Scenario", "response-validation/refusal-is-rejected")]
    public void Validate_RejectsRefusal()
    {
        var result = ResponseValidator.Validate(
            Fixtures.Input("response-validation/refusal-is-rejected.txt", stopReason: StopReason.Refusal));

        ValidationAssert.Rejected(result, ("REFUSED", ""));
    }

    [Fact]
    [Trait("Scenario", "response-validation/fenced-json-is-rejected")]
    public void Validate_RejectsFencedJson()
    {
        var result = Validate("response-validation/fenced-json-is-rejected.txt");

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    [Fact]
    [Trait("Scenario", "response-validation/prose-around-json-is-rejected")]
    public void Validate_RejectsProseAroundJson()
    {
        var result = Validate("response-validation/prose-around-json-is-rejected.txt");

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    [Fact]
    [Trait("Scenario", "response-validation/malformed-json-is-rejected")]
    public void Validate_RejectsMalformedJson()
    {
        var result = Validate("response-validation/malformed-json-is-rejected.txt");

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    [Fact]
    [Trait("Scenario", "response-validation/wrong-type-is-rejected")]
    public void Validate_RejectsWrongType()
    {
        var result = Validate("response-validation/wrong-type-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].line_refs"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/unknown-field-is-rejected")]
    public void Validate_RejectsUnknownField()
    {
        var result = Validate("response-validation/unknown-field-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].confidence"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/closed-set-values-are-matched-case-insensitively")]
    public void Validate_NormalizesClosedSetCasing()
    {
        var result = Validate("response-validation/closed-set-values-are-matched-case-insensitively.json");

        var finding = Assert.Single(ValidationAssert.Accepted(result).Findings);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(FindingCategory.Resource, finding.Category);
        Assert.Equal("critical", finding.Severity.ToContractValue());
        Assert.Equal("resource", finding.Category.ToContractValue());
    }

    [Fact]
    [Trait("Scenario", "response-validation/citation-outside-the-log-is-rejected")]
    public void Validate_RejectsCitationOutsideLog()
    {
        var result = ResponseValidator.Validate(
            Fixtures.Input("response-validation/citation-outside-the-log-is-rejected.json", Fixtures.LongOrdersLog));

        ValidationAssert.Rejected(result, ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"));
        Assert.Equal("Line 340 is not in the log, which has lines 1 to 120.", result.Violations[0].Message);
    }

    [Fact]
    [Trait("Scenario", "response-validation/line-zero-is-rejected")]
    public void Validate_RejectsLineZero()
    {
        var result = Validate("response-validation/line-zero-is-rejected.json");

        ValidationAssert.Rejected(result, ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/paraphrased-evidence-is-rejected")]
    public void Validate_RejectsParaphrasedEvidence()
    {
        var result = Validate("response-validation/paraphrased-evidence-is-rejected.json");

        ValidationAssert.Rejected(result, ("EVIDENCE_NOT_FOUND", "findings[0].evidence"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/evidence-from-an-uncited-line-is-rejected")]
    public void Validate_RejectsEvidenceFromUncitedLine()
    {
        var result = Validate("response-validation/evidence-from-an-uncited-line-is-rejected.json");

        ValidationAssert.Rejected(result, ("EVIDENCE_NOT_FOUND", "findings[0].evidence"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/blank-suggested-action-is-rejected")]
    public void Validate_RejectsBlankSuggestedAction()
    {
        var result = Validate("response-validation/blank-suggested-action-is-rejected.json");

        ValidationAssert.Rejected(result, ("BLANK_FIELD", "findings[0].suggested_action"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/overlong-title-is-rejected")]
    public void Validate_RejectsOverlongTitle()
    {
        var result = Validate("response-validation/overlong-title-is-rejected.json");

        ValidationAssert.Rejected(result, ("FIELD_TOO_LONG", "findings[0].title"));
    }

    [Fact]
    [Trait("Scenario", "response-validation/two-violations-are-both-reported")]
    public void Validate_ReportsEveryViolation()
    {
        var result = Validate("response-validation/two-violations-are-both-reported.json");

        ValidationAssert.Rejected(
            result,
            ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"),
            ("BLANK_FIELD", "findings[1].title"));
    }

    // The tests below pin rules that no scenario names.

    [Fact]
    public void Validate_RejectsJsonThatIsNotAnObject()
    {
        var result = ResponseValidator.Validate(new ValidationInput("[]", StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    [Fact]
    public void Validate_RejectsRepeatedFieldName()
    {
        var response = """{ "summary": "One summary.", "summary": "Another.", "findings": [] }""";

        var result = ResponseValidator.Validate(new ValidationInput(response, StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
        // The message carries the parser's reason, so a corrective request can say what to fix.
        Assert.Contains("'summary'", result.Violations[0].Message, StringComparison.Ordinal);
    }

    // An unpaired UTF-16 surrogate is not text: it gets NOT_JSON instead of an exception, wherever it appears.
    [Theory]
    [InlineData("""{ "summary": "\uD800", "findings": [] }""")]
    [InlineData("""{ "summary": "Fine.", "findings": [], "\uDC00": 1 }""")]
    public void Validate_RejectsEscapedLoneSurrogate(string response)
    {
        var result = ResponseValidator.Validate(new ValidationInput(response, StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    [Fact]
    public void Validate_RejectsUnpairedSurrogateInText()
    {
        var response = "{ \"summary\": \"broken " + '\uD83D' + " text\", \"findings\": [] }";

        var result = ResponseValidator.Validate(new ValidationInput(response, StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("NOT_JSON", ""));
    }

    // A response the model service reports as cut off or refused is rejected even when its text is a valid analysis.
    [Theory]
    [InlineData(StopReason.OutputLimit, "TRUNCATED")]
    [InlineData(StopReason.Refusal, "REFUSED")]
    public void Validate_RejectsIncompleteResponseWithoutReadingIt(StopReason stopReason, string code)
    {
        var result = ResponseValidator.Validate(
            Fixtures.Input("log-analysis/complete-finding-is-accepted.json", stopReason: stopReason));

        ValidationAssert.Rejected(result, (code, ""));
    }

    [Fact]
    public void Validate_RefusesUndefinedStopReason() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ResponseValidator.Validate(Fixtures.Input("log-analysis/complete-finding-is-accepted.json", stopReason: (StopReason)99)));

    [Fact]
    public void Validate_AcceptsCitationOfLastLine()
    {
        var result = ResponseValidator.Validate(WithLineRefs("[120]", "Processed batch 120 of 120", Fixtures.LongOrdersLog));

        Assert.Equal(new[] { 120 }, Assert.Single(ValidationAssert.Accepted(result).Findings).LineRefs);
    }

    [Fact]
    public void Validate_RejectsCitationOneLinePastTheEnd()
    {
        var result = ResponseValidator.Validate(WithLineRefs("[121]", "Processed batch 120 of 120", Fixtures.LongOrdersLog));

        ValidationAssert.Rejected(result, ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"));
    }

    // "Integer" means a whole number, read exactly from the number as written: 12.0 and 1.2e1 are line 12.
    [Theory]
    [InlineData("[12.0]")]
    [InlineData("[1.2e1]")]
    public void Validate_AcceptsWholeNumberWrittenWithFractionOrExponent(string lineRefs)
    {
        var result = ResponseValidator.Validate(WithLineRefs(lineRefs, "Database connection pool exhausted"));

        Assert.Equal(new[] { 12 }, Assert.Single(ValidationAssert.Accepted(result).Findings).LineRefs);
    }

    [Theory]
    [InlineData("[0.99999999999999999999999999999]")]
    [InlineData("[12.000000000000000000000000000000001]")]
    [InlineData("[1e-400]")]
    public void Validate_RejectsNumberWithFractionHoweverSmall(string lineRefs)
    {
        var result = ResponseValidator.Validate(WithLineRefs(lineRefs, "Database connection pool exhausted"));

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].line_refs[0]"));
    }

    [Theory]
    [InlineData("[1e30]")]
    [InlineData("[79228162514264337593543950336]")]
    [InlineData("[-1E400]")]
    public void Validate_RejectsHugeWholeNumberAsOutOfRange(string lineRefs)
    {
        var result = ResponseValidator.Validate(WithLineRefs(lineRefs, "Database connection pool exhausted"));

        ValidationAssert.Rejected(result, ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"));
    }

    [Theory]
    [InlineData("title")]
    [InlineData("evidence")]
    [InlineData("explanation")]
    public void Validate_RejectsBlankFindingText(string field)
    {
        var result = ResponseValidator.Validate(CompleteFindingWith(field, " \t "));

        ValidationAssert.Rejected(result, ("BLANK_FIELD", $"findings[0].{field}"));
    }

    [Fact]
    public void Validate_RejectsBlankSummary()
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["summary"] = string.Empty;

        var result = ResponseValidator.Validate(new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("BLANK_FIELD", "summary"));
    }

    [Fact]
    public void Validate_RejectsEvidenceInDifferentLetterCase()
    {
        var result = ResponseValidator.Validate(CompleteFindingWith("evidence", "database connection pool exhausted"));

        ValidationAssert.Rejected(result, ("EVIDENCE_NOT_FOUND", "findings[0].evidence"));
    }

    [Fact]
    public void Validate_ReportsViolationsOfEveryKindInOrder()
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["findings"]![0]!["title"] = " ";
        response["findings"]![0]!["severity"] = "fatal";
        response["findings"]![0]!["line_refs"] = new JsonArray(340);

        var result = ResponseValidator.Validate(new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(
            result,
            ("SCHEMA_VIOLATION", "findings[0].severity"),
            ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"),
            ("BLANK_FIELD", "findings[0].title"));
    }

    [Fact]
    public void Validate_AcceptsSummaryOf1000Characters()
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["summary"] = string.Concat(Enumerable.Repeat("\U0001F600", 1000));

        var result = ResponseValidator.Validate(new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Accepted(result);
    }

    // Lengths count Unicode code points: each U+1F600 is one character, though .NET stores it as two UTF-16 units.
    [Fact]
    public void Validate_AcceptsTitleOf120Characters()
    {
        var result = ResponseValidator.Validate(CompleteFindingWith("title", string.Concat(Enumerable.Repeat("\U0001F600", 120))));

        ValidationAssert.Accepted(result);
    }

    [Fact]
    public void Validate_RejectsTitleOf121Characters()
    {
        var result = ResponseValidator.Validate(CompleteFindingWith("title", new string('t', 121)));

        ValidationAssert.Rejected(result, ("FIELD_TOO_LONG", "findings[0].title"));
    }

    [Fact]
    public void Validate_RejectsSummaryOf1001Characters()
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["summary"] = new string('s', 1001);

        var result = ResponseValidator.Validate(new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog)));

        ValidationAssert.Rejected(result, ("FIELD_TOO_LONG", "summary"));
    }

    [Fact]
    public void Validate_IgnoresWhitespaceAroundEvidence()
    {
        var result = ResponseValidator.Validate(CompleteFindingWith("evidence", "  Database connection pool exhausted\t"));

        Assert.Equal("  Database connection pool exhausted\t", Assert.Single(ValidationAssert.Accepted(result).Findings).Evidence);
    }

    private static ValidationResult Validate(string response) => ResponseValidator.Validate(Fixtures.Input(response));

    // One finding whose line_refs are written exactly as given, so number literals such as 1.2e1 reach the validator unchanged.
    private static ValidationInput WithLineRefs(string lineRefs, string evidence, string log = Fixtures.OrdersLog) => new(
        $$"""
        {
          "summary": "One finding.",
          "findings": [
            {
              "title": "A finding",
              "severity": "critical",
              "category": "resource",
              "line_refs": {{lineRefs}},
              "evidence": "{{evidence}}",
              "explanation": "Why it matters.",
              "suggested_action": "What to do."
            }
          ]
        }
        """,
        StopReason.Completed,
        Fixtures.LogLines(log));

    // The complete finding of the log-analysis scenarios (citing line 12 of orders-api-40.log) with one field replaced.
    private static ValidationInput CompleteFindingWith(string field, string value)
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["findings"]![0]![field] = value;
        return new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog));
    }
}
