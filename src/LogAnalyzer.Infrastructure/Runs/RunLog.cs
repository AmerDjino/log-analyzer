namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// A run's lines as split at submission, without their line breaks: line N is <c>lines[N]</c> in PostgreSQL and
/// <c>Lines[N - 1]</c> here. One row per run, inserted with the run.
/// </summary>
public sealed class RunLog
{
    public Guid RunId { get; set; }

    public required string[] Lines { get; set; }
}
