using Microsoft.Extensions.Options;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// A run setting outside its allowed range stops the API when it starts, with a message naming the setting. The API runs
/// as a process of its own: see <see cref="ApiProcess"/>.
/// </summary>
public sealed class SettingsValidationTests
{
    [Theory]
    [InlineData("Ingestion:MaxSubmissionBytes", "0")]
    [InlineData("Ingestion:MaxSubmissionBytes", "104857601")]
    [InlineData("Analysis:MaxConcurrentRuns", "0")]
    [InlineData("Analysis:MaxConcurrentRuns", "17")]
    public async Task Startup_RefusesSettingOutsideItsRange(string setting, string value)
    {
        var (exitCode, log) = await ApiProcess.RunUntilExitAsync(
            new Dictionary<string, string> { [setting.Replace(":", "__", StringComparison.Ordinal)] = value },
            TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(nameof(OptionsValidationException), log, StringComparison.Ordinal);
        Assert.Contains(setting, log, StringComparison.Ordinal);
    }
}
