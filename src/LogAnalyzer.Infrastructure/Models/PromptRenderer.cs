using System.Text;
using LogAnalyzer.Core.Processing;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>A request's prompts as text.</summary>
/// <param name="System">The system prompt.</param>
/// <param name="Log">The numbered log, identical in the first and the corrective request, so the second reads it from the cache.</param>
/// <param name="Correction">For the corrective request: what to fix, the rejected response and each violation.</param>
public sealed record RenderedPrompt(string System, string Log, string? Correction);

/// <summary>Turns a prompt template and an analysis request into the text sent to the model.</summary>
public static class PromptRenderer
{
    public static RenderedPrompt Render(PromptTemplate template, AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);
        return new RenderedPrompt(
            template.System,
            RenderLog(request.Lines),
            request.Correction is { } correction ? RenderCorrection(template, correction) : null);
    }

    // Each line is "N: text", N being its number in the log as submitted.
    private static string RenderLog(IReadOnlyList<NumberedLine> lines)
    {
        var log = new StringBuilder("<log>\n");
        foreach (var line in lines)
        {
            log.Append(line.Number).Append(": ").Append(line.Text).Append('\n');
        }

        return log.Append("</log>").ToString();
    }

    private static string RenderCorrection(PromptTemplate template, Correction correction)
    {
        var text = new StringBuilder(template.Correction)
            .Append("\n\n<rejected_response>\n")
            .Append(correction.RejectedResponseText)
            .Append("\n</rejected_response>\n\n<reasons>\n");
        foreach (var violation in correction.Violations)
        {
            text.Append("- ").Append(violation.Code)
                .Append(violation.Path.Length == 0 ? " for the whole response" : $" at {violation.Path}")
                .Append(": ").Append(violation.Message).Append('\n');
        }

        return text.Append("</reasons>").ToString();
    }
}
