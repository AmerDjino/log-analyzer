using Microsoft.Extensions.Options;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>
/// Model settings that cannot work stop the API when it starts, with a message naming the setting. The API runs as a
/// process of its own: see <see cref="ApiProcess"/>.
/// </summary>
public sealed class ModelSettingsTests
{
    [Fact]
    [Trait("Scenario", "model-gateway/missing-key-stops-startup")]
    public async Task Startup_FailsWithoutKeyWhenClaudeIsSelected()
    {
        var (exitCode, log) = await ApiProcess.RunUntilExitAsync(
            new Dictionary<string, string> { ["Model__Provider"] = "Claude" }, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(nameof(OptionsValidationException), log, StringComparison.Ordinal);
        Assert.Contains("Model:Claude:ApiKey", log, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Scenario", "model-gateway/unknown-provider-stops-startup")]
    public async Task Startup_FailsForUnknownProvider()
    {
        var (exitCode, log) = await ApiProcess.RunUntilExitAsync(
            new Dictionary<string, string> { ["Model__Provider"] = "gpt" }, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(nameof(OptionsValidationException), log, StringComparison.Ordinal);
        Assert.Contains("Model:Provider", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_FailsForPromptVersionWithoutTemplates()
    {
        var (exitCode, log) = await ApiProcess.RunUntilExitAsync(
            new Dictionary<string, string>
            {
                ["Model__Provider"] = "Claude",
                ["Model__Claude__ApiKey"] = ClaudeTestSettings.ApiKey,
                ["Model__Claude__PromptVersion"] = "log-analysis/v9",
            },
            TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(nameof(OptionsValidationException), log, StringComparison.Ordinal);
        Assert.Contains("Model:Claude:PromptVersion", log, StringComparison.Ordinal);
        Assert.DoesNotContain(ClaudeTestSettings.ApiKey, log, StringComparison.Ordinal);
    }
}
