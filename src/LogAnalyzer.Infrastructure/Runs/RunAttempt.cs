namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>Whether the validator accepted a model response. Stored in lowercase.</summary>
public enum AttemptOutcome
{
    Accepted,
    Rejected,
}

/// <summary>
/// One reason a response was rejected: the reason code and the path of the failing field. Any path segment that is not a
/// contract field name or an array index is stored as <c>*</c>, because it was written by the model.
/// </summary>
public sealed record RejectionReason(string Code, string Path);

/// <summary>
/// One model response the validator evaluated. It records the outcome and why the response was rejected, never the
/// response text or the violation messages, which quote it.
/// </summary>
public sealed class RunAttempt
{
    public Guid RunId { get; set; }

    /// <summary>1 for the first response, 2 for the corrected one.</summary>
    public int Sequence { get; set; }

    public AttemptOutcome Outcome { get; set; }

    /// <summary>Every violation's code and path; empty exactly when the response was accepted.</summary>
    public required List<RejectionReason> RejectionReasons { get; set; }

    public required string Model { get; set; }

    public required string PromptVersion { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }
}
