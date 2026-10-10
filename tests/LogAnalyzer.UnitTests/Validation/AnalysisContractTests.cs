using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.UnitTests.Validation;

/// <summary>The log-analysis scenarios: what an accepted analysis contains.</summary>
public sealed class AnalysisContractTests
{
    [Fact]
    [Trait("Scenario", "log-analysis/analysis-with-findings-is-accepted")]
    public void Validate_AcceptsAnalysisWithFindings()
    {
        var result = Validate("log-analysis/analysis-with-findings-is-accepted.json");

        var analysis = ValidationAssert.Accepted(result);
        Assert.Equal(
            new[] { "Database connection pool exhausted", "Unhandled NullReferenceException crashed the process" },
            analysis.Findings.Select(finding => finding.Title));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/analysis-without-findings-is-accepted")]
    public void Validate_AcceptsAnalysisWithoutFindings()
    {
        var result = Validate("log-analysis/analysis-without-findings-is-accepted.json");

        var analysis = ValidationAssert.Accepted(result);
        Assert.Equal("The log shows a normal start and orders being stored. No problems were found.", analysis.Summary);
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    [Trait("Scenario", "log-analysis/analysis-without-a-summary-is-rejected")]
    public void Validate_RejectsMissingSummary()
    {
        var result = Validate("log-analysis/analysis-without-a-summary-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "summary"));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/complete-finding-is-accepted")]
    public void Validate_AcceptsCompleteFinding()
    {
        var result = Validate("log-analysis/complete-finding-is-accepted.json");

        var finding = Assert.Single(ValidationAssert.Accepted(result).Findings);
        Assert.Equal("Database connection pool exhausted", finding.Title);
        Assert.Equal(Severity.Critical, finding.Severity);
        Assert.Equal(FindingCategory.Resource, finding.Category);
        Assert.Equal(new[] { 12 }, finding.LineRefs);
        Assert.Equal("Database connection pool exhausted", finding.Evidence);
        Assert.Equal(
            "Every pooled database connection is in use, so new requests cannot reach the database and orders fail to save.",
            finding.Explanation);
        Assert.Equal(
            "Look for connections that are not disposed, then check the Max Pool Size in the connection string.",
            finding.SuggestedAction);
    }

    [Fact]
    [Trait("Scenario", "log-analysis/line-numbers-start-at-1")]
    public void Validate_TreatsLineNumbersAsOneBased()
    {
        var result = Validate("log-analysis/line-numbers-start-at-1.json");

        var finding = Assert.Single(ValidationAssert.Accepted(result).Findings);
        Assert.Equal(new[] { 1 }, finding.LineRefs);
    }

    [Fact]
    [Trait("Scenario", "log-analysis/finding-with-a-missing-field-is-rejected")]
    public void Validate_RejectsFindingWithMissingField()
    {
        var result = Validate("log-analysis/finding-with-a-missing-field-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].suggested_action"));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/finding-without-cited-lines-is-rejected")]
    public void Validate_RejectsFindingWithoutCitedLines()
    {
        var result = Validate("log-analysis/finding-without-cited-lines-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].line_refs"));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/each-defined-severity-is-accepted")]
    public void Validate_AcceptsEachDefinedSeverity()
    {
        var result = Validate("log-analysis/each-defined-severity-is-accepted.json");

        Assert.Equal(
            new[] { Severity.Critical, Severity.Error, Severity.Warning, Severity.Info },
            ValidationAssert.Accepted(result).Findings.Select(finding => finding.Severity));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/unknown-severity-is-rejected")]
    public void Validate_RejectsUnknownSeverity()
    {
        var result = Validate("log-analysis/unknown-severity-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].severity"));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/each-defined-category-is-accepted")]
    public void Validate_AcceptsEachDefinedCategory()
    {
        var result = Validate("log-analysis/each-defined-category-is-accepted.json");

        Assert.Equal(
            new[]
            {
                FindingCategory.Exception, FindingCategory.Timeout, FindingCategory.Dependency, FindingCategory.Resource,
                FindingCategory.Security, FindingCategory.Performance, FindingCategory.Configuration, FindingCategory.Other,
            },
            ValidationAssert.Accepted(result).Findings.Select(finding => finding.Category));
    }

    [Fact]
    [Trait("Scenario", "log-analysis/unknown-category-is-rejected")]
    public void Validate_RejectsUnknownCategory()
    {
        var result = Validate("log-analysis/unknown-category-is-rejected.json");

        ValidationAssert.Rejected(result, ("SCHEMA_VIOLATION", "findings[0].category"));
    }

    private static ValidationResult Validate(string response) => ResponseValidator.Validate(Fixtures.Input(response));
}
