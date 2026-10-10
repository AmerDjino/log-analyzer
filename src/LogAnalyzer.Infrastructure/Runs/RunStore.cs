using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>A run's place in a list of runs, newest first: its submission time, then its id.</summary>
public readonly record struct RunPosition(DateTimeOffset SubmittedAt, Guid Id);

/// <summary>
/// Stores submitted runs, and reads them back for the person who submitted them. Every read goes through
/// <see cref="Owned"/>, which filters on the person's account id in SQL, so another person's run is never found.
/// </summary>
public sealed class RunStore(LogAnalyzerDbContext database, RunQueueSignal queueSignal, TimeProvider timeProvider)
{
    /// <summary>
    /// Stores an accepted submission as a queued run, with its lines and statistics, in one transaction, then wakes the
    /// worker. Returns the new run's id, a UUIDv7 built from the submission time.
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
        queueSignal.Notify();
        return run.Id;
    }

    /// <summary>The person's run with its findings in the model's order, or null when they have no run with this id.</summary>
    public Task<Run?> FindOwnedAsync(Guid userId, Guid runId, CancellationToken cancellationToken) =>
        Owned(userId)
            .Where(run => run.Id == runId)
            .Include(run => run.Findings.OrderBy(finding => finding.Position))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Up to <paramref name="count"/> of the person's runs, newest first by submission time and then id, starting after
    /// <paramref name="after"/>, or with the newest when it is null. Findings are not loaded.
    /// </summary>
    public Task<List<Run>> ListOwnedAsync(Guid userId, RunPosition? after, int count, CancellationToken cancellationToken)
    {
        var runs = Owned(userId);
        if (after is { } position)
        {
            // (submitted_at, id) < (@submittedAt, @id): the same order as the listing index.
            runs = runs.Where(run => EF.Functions.LessThan(
                ValueTuple.Create(run.SubmittedAt, run.Id),
                ValueTuple.Create(position.SubmittedAt, position.Id)));
        }

        return runs
            .OrderByDescending(run => run.SubmittedAt)
            .ThenByDescending(run => run.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    // The ownership filter. Endpoints read runs only through it; the worker uses methods of its own.
    private IQueryable<Run> Owned(Guid userId) =>
        database.Runs.AsNoTracking().Where(run => run.UserId == userId);
}
