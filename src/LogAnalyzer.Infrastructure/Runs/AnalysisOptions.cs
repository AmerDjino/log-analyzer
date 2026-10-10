using System.ComponentModel.DataAnnotations;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>Settings for processing runs: the <c>Analysis</c> configuration section, checked when the API starts.</summary>
public sealed class AnalysisOptions
{
    public const string SectionName = "Analysis";

    /// <summary>How many runs are processed at the same time, each by a loop of the worker. 2 unless configured.</summary>
    [Range(1, 16, ErrorMessage = "Analysis:MaxConcurrentRuns must be between 1 and 16.")]
    public int MaxConcurrentRuns { get; set; } = 2;
}
