using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed partial class AccessPolicyTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // The only endpoints the user-accounts spec allows without a session, by HTTP method and route pattern,
    // so a new method on a public path is still checked.
    private static readonly HashSet<(string Method, string Route)> PublicEndpoints =
    [
        ("GET", "/health"),
        ("POST", "/auth/register"),
        ("POST", "/auth/sign-in"),
        ("GET", "/openapi/{documentName}.json"),
    ];

    [Fact]
    [Trait("Scenario", "user-accounts/non-public-endpoints-refuse-anonymous-requests")]
    public async Task NonPublicEndpoints_RefuseAnonymousRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClientWithoutCookies();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (Method: method, Route: Normalize(endpoint.RoutePattern.RawText))))
            .Where(endpoint => !PublicEndpoints.Contains(endpoint))
            .ToList();

        // Guards against a vacuous pass. Sign-out has no check of its own, so only the default rule can refuse it.
        Assert.Contains(("POST", "/auth/sign-out"), endpoints);

        foreach (var (method, route) in endpoints)
        {
            var path = RouteParameter().Replace(route, Guid.Empty.ToString());
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (method is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request, cancellationToken);

            Assert.True(
                response.StatusCode == HttpStatusCode.Unauthorized,
                $"{method} {path} answered {(int)response.StatusCode} without a session.");
            await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
        }
    }

    private static string Normalize(string? rawText) => "/" + (rawText ?? string.Empty).TrimStart('/');

    // Route values do not matter: an anonymous request must be refused before any lookup.
    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
