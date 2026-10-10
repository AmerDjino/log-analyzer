using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// A run setting outside its allowed range stops the API when it starts, with a message naming the setting. The API runs
/// as a process of its own, as it does in production. Hosted in memory instead, a failed start races the API's own
/// entry point, which disposes the host, against the test host, which still reads its services, so the exception the
/// test host reports depends on timing.
/// </summary>
public sealed class SettingsValidationTests
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("Ingestion:MaxSubmissionBytes", "0")]
    [InlineData("Ingestion:MaxSubmissionBytes", "104857601")]
    [InlineData("Analysis:MaxConcurrentRuns", "0")]
    [InlineData("Analysis:MaxConcurrentRuns", "17")]
    public async Task Startup_RefusesSettingOutsideItsRange(string setting, string value)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { typeof(Program).Assembly.Location },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                // Unreachable: the settings are checked before the database is needed.
                ["ConnectionStrings__Postgres"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
                // Should the API start after all, it listens on a free port rather than failing on one in use.
                ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
                [setting.Replace(":", "__", StringComparison.Ordinal)] = value,
            },
        };

        using var api = Process.Start(start)!;
        var output = api.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = api.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ExitTimeout);
        try
        {
            await api.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            api.Kill(entireProcessTree: true);
            Assert.Fail($"The API was still running {ExitTimeout.TotalSeconds:0} s after it started with {setting}={value}.");
        }

        var log = await output + await error;
        Assert.NotEqual(0, api.ExitCode);
        Assert.Contains(nameof(OptionsValidationException), log, StringComparison.Ordinal);
        Assert.Contains(setting, log, StringComparison.Ordinal);
    }
}
