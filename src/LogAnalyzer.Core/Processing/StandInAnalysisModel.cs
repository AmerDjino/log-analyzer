using System.Text.Json;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Core.Processing;

/// <summary>
/// A deterministic stand-in for a real model: no I/O, and the same reply for the same lines. It flags up to three lines
/// with distinct text, critical first, then error, then warning, judged by the statistics' level rule, and says plainly
/// that no AI model analyzed the log. Its reply goes through the validator like any other.
/// </summary>
public sealed class StandInAnalysisModel : IAnalysisModel
{
    /// <summary>The model name and prompt version its attempts record.</summary>
    public const string Name = "stand-in";

    private const int MaxFindings = 3;

    private const string NoAiModel = "No AI model analyzed this log.";

    private const string Explanation =
        $"The stand-in model flagged this line because of its log level. {NoAiModel} The cause is not explained.";

    private const string SuggestedAction =
        "Read the cited line and the lines around it. Configure an AI model to get an analysis of the cause.";

    private static readonly (LineLevel Level, Severity Severity)[] FlaggedLevels =
    [
        (LineLevel.Critical, Severity.Critical),
        (LineLevel.Error, Severity.Error),
        (LineLevel.Warning, Severity.Warning),
    ];

    public string ConfiguredModel => Name;

    public string ConfiguredPromptVersion => Name;

    public Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var levels = request.Lines.Select(line => LineLevels.Detect(line.Text)).ToList();
        var findings = new List<object>();
        var flaggedTexts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (level, severity) in FlaggedLevels)
        {
            for (var index = 0; index < request.Lines.Count && findings.Count < MaxFindings; index++)
            {
                var line = request.Lines[index];
                var evidence = line.Text.Trim();
                if (levels[index] == level && flaggedTexts.Add(evidence))
                {
                    findings.Add(new
                    {
                        title = $"{level} logged on line {line.Number}",
                        severity = severity.ToContractValue(),
                        category = CategoryOf(evidence).ToContractValue(),
                        line_refs = new[] { line.Number },
                        evidence,
                        explanation = Explanation,
                        suggested_action = SuggestedAction,
                    });
                }
            }
        }

        var summary = findings.Count == 0
            ? $"The stand-in model found no critical, error or warning lines. {NoAiModel}"
            : $"The stand-in model flagged {findings.Count} {(findings.Count == 1 ? "line" : "lines")} by log level, the most serious first. {NoAiModel}";
        var text = JsonSerializer.Serialize(new { summary, findings });
        return Task.FromResult(new ModelReply(text, StopReason.Completed, Name, Name));
    }

    // By keyword only: the stand-in does not interpret the line.
    private static FindingCategory CategoryOf(string text) =>
        text.Contains("exception", StringComparison.OrdinalIgnoreCase) ? FindingCategory.Exception
        : text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || text.Contains("timed out", StringComparison.OrdinalIgnoreCase) ? FindingCategory.Timeout
        : FindingCategory.Other;
}
