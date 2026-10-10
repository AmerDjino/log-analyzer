using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The PostgreSQL-backed API with the real worker and the stand-in model, and a clock that only tests move. While the
/// clock stands still the worker's poll never fires, so only the signal that a submission raises can wake it.
/// </summary>
public sealed class WorkerApiFactory : PostgresApiFactory
{
    /// <summary>How long the worker waits between looks at the queue when nothing wakes it.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(9), TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
