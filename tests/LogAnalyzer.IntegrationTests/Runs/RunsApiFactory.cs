using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The PostgreSQL-backed API set up for run tests: a clock that tests move by hand, starting at 09:00 UTC today, and a
/// submission size limit of 1 MB (1,048,576 bytes).
/// </summary>
public sealed class RunsApiFactory : PostgresApiFactory
{
    public const int MaxSubmissionBytes = 1_048_576;

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(9), TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ingestion:MaxSubmissionBytes"] = MaxSubmissionBytes.ToString(CultureInfo.InvariantCulture),
            }));
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
