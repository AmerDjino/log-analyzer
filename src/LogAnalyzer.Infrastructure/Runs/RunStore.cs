using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Infrastructure.Persistence;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>Stores submitted runs, and reads them back for the person who submitted them.</summary>
public sealed class RunStore(LogAnalyzerDbContext database, TimeProvider timeProvider)
{
    /// <summary>
    /// Stores an accepted submission as a queued run, with its lines and statistics, in one transaction.
    /// Returns the new run's id, a UUIDv7 built from the submission time.
    /// </summary>
    public async Task<Guid> SubmitAsync(
        Guid userId,
        string? fileName,
        int byteCount,
        IReadOnlyList<string> lines,
        LogStatistics statistics,
        CancellationToken cancellationToken)
    {
        var submittedAt = timeProvider.GetUtcNow();
        var timeRange = statistics.TimeRange;
        var run = new Run
        {
            Id = Guid.CreateVersion7(submittedAt),
            UserId = userId,
            FileName = fileName,
            Status = RunStatus.Queued,
            SubmittedAt = submittedAt,
            ByteCount = byteCount,
            LineCount = statistics.LineCount,
            CriticalCount = statistics.Levels.Critical,
            ErrorCount = statistics.Levels.Error,
            WarningCount = statistics.Levels.Warning,
            InfoCount = statistics.Levels.Info,
            DebugCount = statistics.Levels.Debug,
            TraceCount = statistics.Levels.Trace,
            // Stored without a kind: the columns hold times without an offset, and TimeRangeUtc says how to read them.
            TimeRangeStart = timeRange is null ? null : DateTime.SpecifyKind(timeRange.Start, DateTimeKind.Unspecified),
            TimeRangeEnd = timeRange is null ? null : DateTime.SpecifyKind(timeRange.End, DateTimeKind.Unspecified),
            TimeRangeUtc = timeRange?.Utc,
        };

        database.Runs.Add(run);
        database.RunLogs.Add(new RunLog { RunId = run.Id, Lines = [.. lines] });
        await database.SaveChangesAsync(cancellationToken);
        return run.Id;
    }
}
