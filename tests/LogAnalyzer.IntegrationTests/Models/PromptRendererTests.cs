using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;
using LogAnalyzer.Infrastructure.Models;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>What a prompt template and a request turn into, and the templates the API ships.</summary>
public sealed class PromptRendererTests
{
    private static readonly PromptTemplate Template = new("log-analysis/v1", "Analyze the log.", "Fix the rejected response.");

    private static readonly Guid RunId = Guid.CreateVersion7();

    // Lines keep their numbers in the log as submitted, which need not start at 1.
    private static readonly NumberedLine[] Lines =
    [
        new(7, "info: Orders.Api[0] Application started"),
        new(8, "fail: Orders.Api[0] Database connection pool exhausted"),
    ];

    [Fact]
    public void Render_NumbersEachLineWithItsNumber()
    {
        var prompt = PromptRenderer.Render(Template, new AnalysisRequest(RunId, Lines, Correction: null));

        Assert.Equal("Analyze the log.", prompt.System);
        Assert.Contains(
            "7: info: Orders.Api[0] Application started\n8: fail: Orders.Api[0] Database connection pool exhausted\n",
            prompt.Log,
            StringComparison.Ordinal);
        Assert.Null(prompt.Correction);
    }

    [Fact]
    public void Render_RepeatsTheSameSystemAndLogInTheCorrectiveRequest()
    {
        var first = PromptRenderer.Render(Template, new AnalysisRequest(RunId, Lines, Correction: null));
        var corrective = PromptRenderer.Render(
            Template,
            new AnalysisRequest(RunId, Lines, new Correction("{}", [new Violation(ReasonCodes.SchemaViolation, "", "Missing summary.")])));

        Assert.Equal(first.System, corrective.System);
        Assert.Equal(first.Log, corrective.Log);
    }

    [Fact]
    public void Render_QuotesTheRejectedResponseAndEachViolation()
    {
        const string Rejected = """{"summary":"The pool ran dry.","findings":[{"line_refs":[99]}]}""";
        Violation[] violations =
        [
            new(ReasonCodes.LineOutOfRange, "findings[0].line_refs[0]", "Line 99 is past the end of the 8-line log."),
            new(ReasonCodes.SchemaViolation, "findings[0]", "Required properties are missing: title."),
        ];

        var prompt = PromptRenderer.Render(Template, new AnalysisRequest(RunId, Lines, new Correction(Rejected, violations)));

        Assert.NotNull(prompt.Correction);
        Assert.StartsWith("Fix the rejected response.", prompt.Correction, StringComparison.Ordinal);
        Assert.Contains(Rejected, prompt.Correction, StringComparison.Ordinal);
        Assert.All(violations, violation =>
        {
            Assert.Contains(violation.Code, prompt.Correction, StringComparison.Ordinal);
            Assert.Contains(violation.Path, prompt.Correction, StringComparison.Ordinal);
            Assert.Contains(violation.Message, prompt.Correction, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void EmbeddedTemplates_HoldVersionOne()
    {
        var template = new EmbeddedPromptTemplates().Find("log-analysis/v1");

        Assert.NotNull(template);
        Assert.Equal("log-analysis/v1", template.Version);
        Assert.Contains("data", template.System, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(template.Correction));
    }

    [Fact]
    public void EmbeddedTemplates_HaveNoOtherVersion()
    {
        Assert.Null(new EmbeddedPromptTemplates().Find("log-analysis/v2"));
        Assert.Null(new EmbeddedPromptTemplates().Find("log-analysis"));
    }
}
