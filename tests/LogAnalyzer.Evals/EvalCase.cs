using System.Text.Json;
using System.Text.Json.Serialization;
using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.Evals;

/// <summary>
/// An eval case, as stored in <c>Cases/&lt;capability&gt;/&lt;scenario&gt;.json</c>: the golden log it analyzes and the graders
/// its analysis must pass.
/// </summary>
public sealed class EvalCase
{
    // The same line breaks on every machine, so the generator writes the same case files everywhere.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The log's file name, in <c>GoldenLogs/&lt;capability&gt;/</c>.</summary>
    public required string Log { get; init; }

    public required List<Grader> Graders { get; init; }

    public static EvalCase Parse(string json) => JsonSerializer.Deserialize<EvalCase>(json, Json)!;

    public static string Serialize(EvalCase evalCase) => JsonSerializer.Serialize(evalCase, Json) + "\n";
}

public static class GraderKinds
{
    /// <summary>Counts the findings that cite one of the lines and match the categories and severities.</summary>
    public const string Findings = "findings";

    /// <summary>The analysis has no findings.</summary>
    public const string NoFindings = "noFindings";

    /// <summary>A judge model answers a yes-or-no question about the summary.</summary>
    public const string Summary = "summary";
}

/// <summary>One check of an analysis. Which fields apply depends on <see cref="Kind"/>.</summary>
public sealed class Grader
{
    public required string Kind { get; init; }

    /// <summary>For findings: line ranges, each [from, to] inclusive. A finding counts when it cites a line in one.</summary>
    public List<int[]>? Lines { get; init; }

    /// <summary>For findings: the categories that count; any when null.</summary>
    public string[]? Categories { get; init; }

    /// <summary>For findings: the severities that count; any when null.</summary>
    public string[]? Severities { get; init; }

    /// <summary>For findings: the fewest findings that may count; 0 when null.</summary>
    public int? Min { get; init; }

    /// <summary>For findings: the most findings that may count; no limit when null.</summary>
    public int? Max { get; init; }

    /// <summary>For summary: the question the judge answers.</summary>
    public string? Question { get; init; }

    /// <summary>For summary: the answer that passes, yes or no.</summary>
    public string? Expected { get; init; }

    /// <summary>For findings and noFindings: why the analysis fails this grader, or null when it passes.</summary>
    public string? GradeFindings(Analysis analysis)
    {
        switch (Kind)
        {
            case GraderKinds.NoFindings:
                return analysis.Findings.Count == 0 ? null : $"Expected no findings, got {analysis.Findings.Count}.";
            case GraderKinds.Findings:
                var counted = analysis.Findings.Count(finding =>
                    finding.LineRefs.Any(line => Lines!.Any(range => line >= range[0] && line <= range[1]))
                    && (Categories is null || Categories.Contains(finding.Category.ToContractValue()))
                    && (Severities is null || Severities.Contains(finding.Severity.ToContractValue())));
                if (counted < (Min ?? 0) || counted > (Max ?? int.MaxValue))
                {
                    return $"Expected {Min ?? 0} to {(Max is { } max ? max.ToString() : "any number of")} findings citing "
                        + $"{string.Join(", ", Lines!.Take(3).Select(range => $"{range[0]}-{range[1]}"))}{(Lines!.Count > 3 ? ", ..." : string.Empty)}"
                        + $"{(Categories is null ? string.Empty : $" in {string.Join("/", Categories)}")}"
                        + $"{(Severities is null ? string.Empty : $" with severity {string.Join("/", Severities)}")}, got {counted}. "
                        + $"Findings: {string.Join("; ", analysis.Findings.Select(finding => $"{finding.Severity.ToContractValue()} {finding.Category.ToContractValue()} [{string.Join(",", finding.LineRefs.Take(5))}] {finding.Title}"))}";
                }

                return null;
            default:
                throw new InvalidOperationException($"Grader kind '{Kind}' does not grade findings.");
        }
    }
}
