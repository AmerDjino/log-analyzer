using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.IntegrationTests;

/// <summary>
/// The PostgreSQL-backed API with a clock that tests move by hand, for rules that depend on time such as idle sessions.
/// The clock starts at 09:00 UTC today.
/// </summary>
public sealed class TimedPostgresApiFactory : PostgresApiFactory
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(9), TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
