namespace LogAnalyzer.Core.Processing;

/// <summary>
/// Why a run failed: a closed set, which docs/runs-api.md explains. The validator's reason codes stay on the attempts and
/// are never a run's failure reason.
/// </summary>
public static class FailureReasons
{
    /// <summary>The model's response and its corrected response were both rejected.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>The model service failed or did not answer in time, after the adapter's retries.</summary>
    public const string ModelUnavailable = "MODEL_UNAVAILABLE";

    /// <summary>The model service refused the request in a way that retrying cannot fix, such as a wrong API key.</summary>
    public const string ModelConfigurationError = "MODEL_CONFIGURATION_ERROR";

    /// <summary>The API stopped while it was processing the run.</summary>
    public const string Interrupted = "INTERRUPTED";

    /// <summary>An unexpected error. The server log has the details, with the run's id.</summary>
    public const string InternalError = "INTERNAL_ERROR";

    public static IReadOnlyList<string> All { get; } =
        [ValidationFailed, ModelUnavailable, ModelConfigurationError, Interrupted, InternalError];
}
