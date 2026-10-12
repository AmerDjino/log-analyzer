using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LogAnalyzer.IntegrationTests;

/// <summary>Hosts the API with a connection string that points at a port where nothing listens.</summary>
public sealed class UnreachableDatabaseApiFactory : WebApplicationFactory<Program>
{
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=5";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = UnreachableConnectionString,
                // The stand-in, whatever the developer's user secrets or environment choose, so no test calls Anthropic.
                ["Model:Provider"] = "StandIn",
            }));
}
