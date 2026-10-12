using LogAnalyzer.Core.Validation;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Maps the Messages API's stop reason onto the validator's, as add-analysis-contract recorded. Only <c>end_turn</c> is a
/// complete answer. Any reason not listed, including one added to the API later, counts as cut off, so it is never
/// accepted, and the pipeline's correction handles it like an answer cut off at the output limit.
/// </summary>
public static class ClaudeStopReasons
{
    public static StopReason Map(string? stopReason) => stopReason switch
    {
        "end_turn" => StopReason.Completed,
        "refusal" => StopReason.Refusal,
        _ => StopReason.OutputLimit,
    };
}
