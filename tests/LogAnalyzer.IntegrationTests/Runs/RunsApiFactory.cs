using System.Globalization;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Infrastructure.Runs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The PostgreSQL-backed API set up for run tests: a clock that tests move by hand, starting at 09:00 UTC today, a
/// submission size limit of 1 MB (1,048,576 bytes), and <see cref="ScriptedAnalysisModel"/> as the model. The worker
/// is removed: a test submits, calls <see cref="ProcessQueuedRunsAsync"/> and reads, without waiting.
/// </summary>
public sealed class RunsApiFactory : PostgresApiFactory
{
    public const int MaxSubmissionBytes = 1_048_576;

    public RunsApiFactory() => Model = new ScriptedAnalysisModel(Clock);

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(9), TimeSpan.Zero));

    public ScriptedAnalysisModel Model { get; }

    /// <summary>Processes queued runs one at a time, oldest first, as the worker does, until none is left.</summary>
    public async Task ProcessQueuedRunsAsync(CancellationToken cancellationToken)
    {
        var processor = Services.GetRequiredService<RunProcessor>();
        while (await processor.ProcessNextAsync(cancellationToken))
        {
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ingestion:MaxSubmissionBytes"] = MaxSubmissionBytes.ToString(CultureInfo.InvariantCulture),
            }));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IAnalysisModel>();
            services.AddSingleton<IAnalysisModel>(Model);
            foreach (var worker in services.Where(IsRunWorker).ToList())
            {
                services.Remove(worker);
            }
        });
    }

    private static bool IsRunWorker(ServiceDescriptor service) =>
        service.ServiceType == typeof(IHostedService) && service.ImplementationType?.Name == "RunWorker";
}
