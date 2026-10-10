using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Metadata;
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
                .Select(method => (
                    Method: method,
                    Route: Normalize(endpoint.RoutePattern.RawText),
                    ContentType: endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.FirstOrDefault())))
            .Where(endpoint => !PublicEndpoints.Contains((endpoint.Method, endpoint.Route)))
            .ToList();

        // Guards against a vacuous pass. Sign-out has no check of its own, so only the default rule can refuse it.
        Assert.Contains(endpoints, endpoint => endpoint is ("POST", "/auth/sign-out", _));

        foreach (var (method, route, contentType) in endpoints)
        {
            var path = RouteParameter().Replace(route, Guid.Empty.ToString());
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            // A body of the media type the endpoint declares; routing answers any other with 415, before the session check.
            if (contentType is not null || method is "POST" or "PUT" or "PATCH")
            {
                request.Content = new StringContent("{}", Encoding.UTF8, contentType ?? "application/json");
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
