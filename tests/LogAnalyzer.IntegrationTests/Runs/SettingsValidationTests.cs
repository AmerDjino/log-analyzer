using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>A run setting outside its allowed range stops the API when it starts, with a message naming the setting.</summary>
public sealed class SettingsValidationTests
{
    [Theory]
    [InlineData("Ingestion:MaxSubmissionBytes", "0")]
    [InlineData("Ingestion:MaxSubmissionBytes", "104857601")]
    [InlineData("Analysis:MaxConcurrentRuns", "0")]
    [InlineData("Analysis:MaxConcurrentRuns", "17")]
    public void Startup_RefusesSettingOutsideItsRange(string setting, string value)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Unreachable: the test needs no database.
                    ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
                    [setting] = value,
                })));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(setting, exception.Message, StringComparison.Ordinal);
    }
}
