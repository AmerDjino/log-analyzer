using LogAnalyzer.Core.Validation;
using LogAnalyzer.Infrastructure.Models;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>How the Messages API's stop reasons reach the validator. Only end_turn is a complete answer.</summary>
public sealed class ClaudeStopReasonsTests
{
    [Theory]
    [InlineData("end_turn", StopReason.Completed)]
    [InlineData("max_tokens", StopReason.OutputLimit)]
    [InlineData("model_context_window_exceeded", StopReason.OutputLimit)]
    [InlineData("refusal", StopReason.Refusal)]
    [InlineData("stop_sequence", StopReason.OutputLimit)]
    [InlineData("tool_use", StopReason.OutputLimit)]
    [InlineData("pause_turn", StopReason.OutputLimit)]
    [InlineData("a_reason_added_later", StopReason.OutputLimit)]
    [InlineData("", StopReason.OutputLimit)]
    [InlineData(null, StopReason.OutputLimit)]
    public void Map_TreatsEverythingButEndTurnAsIncomplete(string? stopReason, StopReason expected)
    {
        Assert.Equal(expected, ClaudeStopReasons.Map(stopReason));
    }
}
