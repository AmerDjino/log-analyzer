using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>Where a run is: queued, then running, then completed or failed. Stored and returned in lowercase.</summary>
public enum RunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

/// <summary>
/// One submitted log and its analysis. The statistics are computed at submission and never change. The summary, the
/// highest severity and the findings exist only once the run has completed.
/// </summary>
public sealed class Run
{
    /// <summary>A UUIDv7 built from the submission time.</summary>
    public Guid Id { get; set; }

    /// <summary>The account that submitted the run; only that person can read it.</summary>
    public Guid UserId { get; set; }

    /// <summary>The cleaned name of an uploaded file; null for a paste.</summary>
    public string? FileName { get; set; }

    public RunStatus Status { get; set; }

    /// <summary>Why the run failed, one of the failure reasons in docs/runs-api.md; null unless it failed.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>The size of the submitted body, in bytes.</summary>
    public int ByteCount { get; set; }

    public int LineCount { get; set; }

    public int CriticalCount { get; set; }

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }

    public int InfoCount { get; set; }

    public int DebugCount { get; set; }

    public int TraceCount { get; set; }

    /// <summary>
    /// The earliest timestamp in the log, without a kind: a UTC time when <see cref="TimeRangeUtc"/> is true, otherwise
    /// the time as the log wrote it. Null, like the end and the flag, when no line has a timestamp.
    /// </summary>
    public DateTime? TimeRangeStart { get; set; }

    /// <summary>The latest timestamp in the log, read like <see cref="TimeRangeStart"/>.</summary>
    public DateTime? TimeRangeEnd { get; set; }

    public bool? TimeRangeUtc { get; set; }

    /// <summary>The accepted analysis' summary; present exactly when the run completed.</summary>
    public string? Summary { get; set; }

    /// <summary>The most serious severity among the findings; null until the run completes, and without findings.</summary>
    public Severity? HighestSeverity { get; set; }

    /// <summary>The accepted findings, in the model's order.</summary>
    public List<RunFinding> Findings { get; } = [];

    /// <summary>The model responses the validator evaluated, in order.</summary>
    public List<RunAttempt> Attempts { get; } = [];
}
