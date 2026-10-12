using System.Collections.Concurrent;
using System.Text.Json;

namespace LogAnalyzer.Evals;

/// <summary>
/// Writes <c>eval-report.json</c> next to the test assembly: per case, its pass rate, and per run its attempts' outcomes,
/// rejection reasons, tokens and latency. It is rewritten after each case, so it holds every case run so far.
/// </summary>
public static class EvalReport
{
    private static readonly ConcurrentDictionary<string, object> Cases = new(StringComparer.Ordinal);

    private static readonly Lock WriteLock = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Adds the case's runs and rewrites the report. Returns the report's path.</summary>
    public static string Record(string scenario, IReadOnlyList<EvalRun> runs)
    {
        Cases[scenario] = new
        {
            passes = runs.Count(run => run.Passed),
            runs = runs.Count,
            inputTokens = runs.Sum(run => run.Attempts.Sum(attempt => attempt.Usage.InputTokens)),
            outputTokens = runs.Sum(run => run.Attempts.Sum(attempt => attempt.Usage.OutputTokens)),
            results = runs.Select(run => new
            {
                passed = run.Passed,
                failure = run.Failure,
                seconds = Math.Round(run.Elapsed.TotalSeconds, 1),
                attempts = run.Attempts.Select(attempt => new
                {
                    accepted = attempt.IsAccepted,
                    reasons = attempt.Violations.Select(violation => $"{violation.Code} {violation.Path}"),
                    model = attempt.Model,
                    promptVersion = attempt.PromptVersion,
                    inputTokens = attempt.Usage.InputTokens,
                    outputTokens = attempt.Usage.OutputTokens,
                    transportRetries = attempt.Usage.TransportRetries,
                    requestId = attempt.Usage.RequestId,
                    seconds = Math.Round((attempt.FinishedAt - attempt.StartedAt).TotalSeconds, 1),
                }),
            }),
        };

        var path = Path.Combine(AppContext.BaseDirectory, "eval-report.json");
        lock (WriteLock)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(
                new
                {
                    generatedAt = DateTimeOffset.UtcNow,
                    model = EvalSettings.Model,
                    effort = EvalSettings.Effort,
                    runsPerCase = LogAnalysisEvals.RunsPerCase,
                    passesNeeded = LogAnalysisEvals.PassesNeeded,
                    cases = Cases.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToDictionary(entry => entry.Key, entry => entry.Value),
                },
                Json));
        }

        return path;
    }
}
