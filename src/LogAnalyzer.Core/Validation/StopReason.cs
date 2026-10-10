namespace LogAnalyzer.Core.Validation;

/// <summary>Why the model stopped writing its response, as the model service reports it.</summary>
public enum StopReason
{
    /// <summary>The model finished its answer.</summary>
    Completed,

    /// <summary>The model reached its output limit, so the response is cut off.</summary>
    OutputLimit,

    /// <summary>The model declined to answer.</summary>
    Refusal,
}
