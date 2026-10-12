using System.Diagnostics;

namespace LogAnalyzer.IntegrationTests;

/// <summary>
/// Starts the API as a process of its own, as it runs in production, for tests of a start that must fail. Hosted in
/// memory instead, a failed start races the API's own entry point, which disposes the host, against the test host, which
/// still reads its services, so the exception the test host reports depends on timing.
/// </summary>
public static class ApiProcess
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    // Inherited settings that would change what a test configures: a developer's own model settings or key.
    private static readonly string[] InheritedSettingPrefixes = ["Model__", "ANTHROPIC_"];

    /// <summary>
    /// Starts the API with the given settings, in the Production environment so user secrets are not read, and waits until
    /// it exits. Fails the test when it is still running after 60 seconds. Returns its exit code and everything it wrote.
    /// </summary>
    /// <param name="settings">Environment variables, such as <c>Model__Provider</c>.</param>
    public static async Task<(int ExitCode, string Output)> RunUntilExitAsync(
        IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { typeof(Program).Assembly.Location },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var name in start.Environment.Keys.Where(IsInheritedSetting).ToList())
        {
            start.Environment.Remove(name);
        }

        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        // Unreachable: settings are checked before the database is needed.
        start.Environment["ConnectionStrings__Postgres"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none";
        // Should the API start after all, it listens on a free port rather than failing on one in use.
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        foreach (var (name, value) in settings)
        {
            start.Environment[name] = value;
        }

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
            Assert.Fail(
                $"The API was still running {ExitTimeout.TotalSeconds:0} s after it started with {string.Join(", ", settings.Keys)}.");
        }

        return (api.ExitCode, await output + await error);
    }

    private static bool IsInheritedSetting(string name) =>
        InheritedSettingPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
