using LogAnalyzer.Core.Contract;
using LogAnalyzer.Infrastructure.Runs;

namespace LogAnalyzer.Api.Runs;

/// <summary>The answer to an accepted submission: the new run's id and its status, queued.</summary>
internal sealed record SubmissionResponse(Guid Id, RunStatus Status);

/// <summary>
/// A run as its owner reads it. The statistics are there for every status; the summary and the findings only once the
/// run has completed, and the failure reason only once it has failed.
/// </summary>
internal sealed record RunResponse(
    Guid Id,
    RunStatus Status,
    string? FileName,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? FailureReason,
    RunStatisticsResponse Statistics,
    string? Summary,
    IReadOnlyList<FindingResponse>? Findings)
{
    public static RunResponse From(Run run) => new(
        run.Id,
        run.Status,
        run.FileName,
        run.SubmittedAt,
        run.StartedAt,
        run.FinishedAt,
        run.FailureReason,
        RunStatisticsResponse.From(run),
        run.Summary,
        run.Status == RunStatus.Completed ? run.Findings.Select(FindingResponse.From).ToList() : null);
}

/// <summary>Statistics computed by code when the log was submitted.</summary>
internal sealed record RunStatisticsResponse(int LineCount, int ByteCount, LevelCountsResponse Levels, TimeRangeResponse? TimeRange)
{
    public static RunStatisticsResponse From(Run run) => new(
        run.LineCount,
        run.ByteCount,
        new LevelCountsResponse(run.CriticalCount, run.ErrorCount, run.WarningCount, run.InfoCount, run.DebugCount, run.TraceCount),
        run is { TimeRangeStart: { } start, TimeRangeEnd: { } end, TimeRangeUtc: { } utc }
            ? new TimeRangeResponse(AsWritten(start, utc), AsWritten(end, utc), utc)
            : null);

    // UTC times are written with a Z, times without an offset with none.
    private static DateTime AsWritten(DateTime time, bool utc) =>
        DateTime.SpecifyKind(time, utc ? DateTimeKind.Utc : DateTimeKind.Unspecified);
}

/// <summary>The number of lines at each level.</summary>
internal sealed record LevelCountsResponse(int Critical, int Error, int Warning, int Info, int Debug, int Trace);

/// <summary>The earliest and latest timestamps: UTC instants when <paramref name="Utc"/> is true, otherwise as written.</summary>
internal sealed record TimeRangeResponse(DateTime Start, DateTime End, bool Utc);

/// <summary>One validated finding, in the order the model gave it.</summary>
internal sealed record FindingResponse(
    string Title,
    Severity Severity,
    FindingCategory Category,
    IReadOnlyList<int> LineRefs,
    string Evidence,
    string Explanation,
    string SuggestedAction)
{
    public static FindingResponse From(RunFinding finding) => new(
        finding.Title,
        finding.Severity,
        finding.Category,
        finding.LineRefs,
        finding.Evidence,
        finding.Explanation,
        finding.SuggestedAction);
}

/// <summary>A page of runs, newest first. <paramref name="NextCursor"/> requests the next page, and is null on the last.</summary>
internal sealed record RunPageResponse(IReadOnlyList<RunListItemResponse> Items, string? NextCursor);

/// <summary>A run in a list. <paramref name="HighestSeverity"/> is null until the run completes, and without findings.</summary>
internal sealed record RunListItemResponse(
    Guid Id,
    string? FileName,
    DateTimeOffset SubmittedAt,
    RunStatus Status,
    Severity? HighestSeverity)
{
    public static RunListItemResponse From(Run run) =>
        new(run.Id, run.FileName, run.SubmittedAt, run.Status, run.HighestSeverity);
}
