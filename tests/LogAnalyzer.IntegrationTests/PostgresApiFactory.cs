using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace LogAnalyzer.IntegrationTests;

/// <summary>
/// Hosts the API in memory against a throwaway PostgreSQL container.
/// Needs a running Docker engine; without one, the fixture fails with Testcontainers' "Docker is not running" error.
/// </summary>
public class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same major version as docker-compose.yml.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public PostgresApiFactory()
    {
        // https, so the client keeps Secure cookies and sends them back.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    /// <summary>Connection string of the throwaway database, for tests that inspect it with SQL.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>A client that keeps no cookies, for sending a session cookie by hand or none at all.</summary>
    public HttpClient CreateClientWithoutCookies() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = ClientOptions.BaseAddress, HandleCookies = false });

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
