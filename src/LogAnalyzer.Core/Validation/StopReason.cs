namespace LogAnalyzer.Core.Validation;

/// <summary>
/// Why the model stopped writing its response, as the model service reports it. Any other reason the service gives
/// means there is no complete answer, so it must not be passed on as <see cref="Completed"/>.
/// </summary>
public enum StopReason
{
    /// <summary>The model finished its answer.</summary>
    Completed,

    /// <summary>The model reached its output limit or its context window, so the response is cut off.</summary>
    OutputLimit,

    /// <summary>The model declined to answer.</summary>
    Refusal,
}
