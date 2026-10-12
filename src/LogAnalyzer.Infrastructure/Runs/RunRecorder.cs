using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// Records a run's progress in the database. Each method saves once, so its writes are one transaction. An attempt keeps
/// each violation's code and masked path, never the response text or the violation messages, which quote it.
/// </summary>
internal sealed class RunRecorder(LogAnalyzerDbContext database, TimeProvider timeProvider) : IRunRecorder
{
    public async Task RecordRejectedAttemptAsync(Guid runId, AttemptRecord attempt, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        database.RunAttempts.Add(ToEntity(runId, attempt));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        Guid runId, AttemptRecord acceptedAttempt, Analysis analysis, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();
        var run = await database.Runs.SingleAsync(run => run.Id == runId, cancellationToken);
        run.Status = RunStatus.Completed;
        run.FinishedAt = timeProvider.GetUtcNow();
        run.Summary = WithoutNul(analysis.Summary);
        // Severity lists the most serious first.
        run.HighestSeverity = analysis.Findings.Count == 0 ? null : analysis.Findings.Min(finding => finding.Severity);
        database.RunAttempts.Add(ToEntity(runId, acceptedAttempt));
        database.RunFindings.AddRange(analysis.Findings.Select((finding, position) => new RunFinding
        {
            RunId = runId,
            Position = position,
            Title = WithoutNul(finding.Title),
            Severity = finding.Severity,
            Category = finding.Category,
            LineRefs = [.. finding.LineRefs],
            Evidence = finding.Evidence,
            Explanation = WithoutNul(finding.Explanation),
            SuggestedAction = WithoutNul(finding.SuggestedAction),
        }));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        Guid runId, string failureReason, AttemptRecord? rejectedAttempt, CancellationToken cancellationToken)
    {
        // A write cancelled at shutdown can leave its changes tracked; none of them may be saved with the failure.
        database.ChangeTracker.Clear();
        var run = await database.Runs.SingleAsync(run => run.Id == runId, cancellationToken);
        run.Status = RunStatus.Failed;
        run.FailureReason = failureReason;
        run.FinishedAt = timeProvider.GetUtcNow();
        if (rejectedAttempt is not null)
        {
            database.RunAttempts.Add(ToEntity(runId, rejectedAttempt));
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    // PostgreSQL text cannot hold U+0000, which the validator does not reject, so it is left out rather than failing a run
    // whose response was accepted. Evidence never has one: it appears on a cited line, and logs with NUL are refused.
    private static string WithoutNul(string text) => text.Replace("\0", string.Empty, StringComparison.Ordinal);

    private static RunAttempt ToEntity(Guid runId, AttemptRecord attempt) => new()
    {
        RunId = runId,
        Sequence = attempt.Sequence,
        Outcome = attempt.IsAccepted ? AttemptOutcome.Accepted : AttemptOutcome.Rejected,
        RejectionReasons =
        [
            .. attempt.Violations.Select(violation => new RejectionReason(violation.Code, RejectionPaths.Mask(violation.Path))),
        ],
        Model = attempt.Model,
        PromptVersion = attempt.PromptVersion,
        StartedAt = attempt.StartedAt,
        FinishedAt = attempt.FinishedAt,
        InputTokens = attempt.Usage.InputTokens,
        OutputTokens = attempt.Usage.OutputTokens,
        TransportRetries = attempt.Usage.TransportRetries,
        RequestId = attempt.Usage.RequestId,
    };
}
