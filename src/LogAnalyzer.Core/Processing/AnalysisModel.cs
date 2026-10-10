using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Core.Processing;

/// <summary>A log line with its number in the log as submitted, from 1.</summary>
public readonly record struct NumberedLine(int Number, string Text);

/// <summary>What a corrected response must fix: the rejected response, and every reason it was rejected.</summary>
public sealed record Correction(string RejectedResponseText, IReadOnlyList<Violation> Violations);

/// <summary>A request to analyze a run's log.</summary>
/// <param name="Lines">The lines to analyze, each with its original number.</param>
/// <param name="Correction">Set for the second attempt, after the first response was rejected.</param>
public sealed record AnalysisRequest(Guid RunId, IReadOnlyList<NumberedLine> Lines, Correction? Correction);

/// <summary>A model's response: its text exactly as returned, why the model stopped, and what produced it.</summary>
public sealed record ModelReply(string Text, StopReason StopReason, string Model, string PromptVersion);

/// <summary>
/// A model that analyzes logs. Its reply is text, so every reply goes through the validator before any part of it is
/// used. A call that yields no reply throws <see cref="ModelCallException"/>.
/// </summary>
public interface IAnalysisModel
{
    Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken);
}

/// <summary>A model call that yielded no response to validate. Its run fails with <see cref="FailureReason"/>.</summary>
public sealed class ModelCallException : Exception
{
    public ModelCallException(string failureReason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        if (failureReason is not (FailureReasons.ModelUnavailable or FailureReasons.ModelConfigurationError))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureReason), failureReason, "A model call fails with MODEL_UNAVAILABLE or MODEL_CONFIGURATION_ERROR.");
        }

        FailureReason = failureReason;
    }

    /// <summary>
    /// MODEL_UNAVAILABLE for a failure that may pass, including a timeout, once the adapter's retries are used up;
    /// MODEL_CONFIGURATION_ERROR when retrying cannot help.
    /// </summary>
    public string FailureReason { get; }
}
