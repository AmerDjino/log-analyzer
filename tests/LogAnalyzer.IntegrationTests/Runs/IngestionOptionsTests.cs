using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>A size limit outside 1 byte to 100 MB stops the API when it starts, with a message naming the setting.</summary>
public sealed class IngestionOptionsTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("104857601")]
    public void Startup_RefusesSizeLimitOutsideTheAllowedRange(string maxSubmissionBytes)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Unreachable: the test needs no database.
                    ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
                    ["Ingestion:MaxSubmissionBytes"] = maxSubmissionBytes,
                })));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Ingestion:MaxSubmissionBytes", exception.Message, StringComparison.Ordinal);
    }
}
