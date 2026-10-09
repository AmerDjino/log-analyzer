using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace LogAnalyzer.IntegrationTests;

/// <summary>
/// Hosts the API in memory against a throwaway PostgreSQL container.
/// Needs a running Docker engine; without one, the fixture fails with Testcontainers' "Docker is not running" error.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same major version as docker-compose.yml.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync() =>
        await _postgres.StartAsync(TestContext.Current.CancellationToken);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            }));
}
