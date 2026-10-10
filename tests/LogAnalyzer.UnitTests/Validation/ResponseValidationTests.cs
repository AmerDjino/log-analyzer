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
    }

    [Fact]
    [Trait("Scenario", "response-validation/citation-outside-the-log-is-rejected")]
    public void Validate_RejectsCitationOutsideLog()
    {
        var result = ResponseValidator.Validate(
            Fixtures.Input("response-validation/citation-outside-the-log-is-rejected.json", Fixtures.LongOrdersLog));

        ValidationAssert.Rejected(result, ("LINE_OUT_OF_RANGE", "findings[0].line_refs[0]"));
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

    // The complete finding of the log-analysis scenarios (citing line 12 of orders-api-40.log) with one field replaced.
    private static ValidationInput CompleteFindingWith(string field, string value)
    {
        var response = JsonNode.Parse(Fixtures.Response("log-analysis/complete-finding-is-accepted.json"))!;
        response["findings"]![0]![field] = value;
        return new ValidationInput(response.ToJsonString(), StopReason.Completed, Fixtures.LogLines(Fixtures.OrdersLog));
    }
}
