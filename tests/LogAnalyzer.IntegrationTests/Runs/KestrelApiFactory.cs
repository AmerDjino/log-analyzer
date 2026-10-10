using System.Globalization;
using System.Net.Http.Json;
using LogAnalyzer.IntegrationTests.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The PostgreSQL-backed API on a real Kestrel server instead of the in-memory test server, for what only Kestrel does,
/// such as reading chunked bodies and enforcing its request body size limit. The submission size limit is 1 MB.
/// </summary>
public sealed class KestrelApiFactory : PostgresApiFactory
{
    public KestrelApiFactory() => UseKestrel(0);

    /// <summary>
    /// Registers a new account and returns a client of the Kestrel server, signed in with it. The server listens on
    /// plain HTTP, where a client keeps no Secure cookie, so this client sends the session cookie itself.
    /// </summary>
    public async Task<HttpClient> CreateSignedInClientAsync(CancellationToken cancellationToken)
    {
        StartServer();
        var address = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        var client = new HttpClient(new SocketsHttpHandler { UseCookies = false }) { BaseAddress = new Uri(address) };
        var credentials = new { email = AccountsClient.NewEmail(), password = AccountsClient.Password };
        using (var registered = await client.PostAsJsonAsync("/auth/register", credentials, cancellationToken))
        {
            registered.EnsureSuccessStatusCode();
        }

        using var signedIn = await client.PostAsJsonAsync("/auth/sign-in", credentials, cancellationToken);
        signedIn.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionCookie.Name}={SessionCookie.RequireValue(signedIn)}");
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ingestion:MaxSubmissionBytes"] = RunsApiFactory.MaxSubmissionBytes.ToString(CultureInfo.InvariantCulture),
            }));
    }
}
