using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed partial class AccessPolicyTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // The only endpoints the user-accounts spec allows without a session.
    private static readonly HashSet<string> PublicRoutes =
    [
        "/health",
        "/auth/register",
        "/auth/sign-in",
        "/openapi/{documentName}.json",
    ];

    [Fact]
    [Trait("Scenario", "user-accounts/non-public-endpoints-refuse-anonymous-requests")]
    public async Task NonPublicEndpoints_RefuseAnonymousRequests()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClientWithoutCookies();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => !PublicRoutes.Contains(Normalize(endpoint.RoutePattern.RawText)))
            .ToList();

        // Guards against a vacuous pass: /auth/me at least must be checked.
        Assert.Contains(endpoints, endpoint => Normalize(endpoint.RoutePattern.RawText) == "/auth/me");

        foreach (var endpoint in endpoints)
        {
            var path = RouteParameter().Replace(Normalize(endpoint.RoutePattern.RawText), Guid.Empty.ToString());
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
            {
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
    }

    private static string Normalize(string? rawText) => "/" + (rawText ?? string.Empty).TrimStart('/');

    // Route values do not matter: an anonymous request must be refused before any lookup.
    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
