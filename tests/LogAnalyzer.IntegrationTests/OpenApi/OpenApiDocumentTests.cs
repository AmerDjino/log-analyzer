using System.Net;
using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.OpenApi;

/// <summary>
/// The OpenAPI document that the Angular API client is generated from. It is served only in Development,
/// which is the test host's environment.
/// </summary>
public sealed class OpenApiDocumentTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // Every answer of each account endpoint that docs/accounts-api.md lists, the success first.
    public static TheoryData<string, string, string, string[]> AccountOperations => new()
    {
        { "/auth/register", "post", "201", ["400", "409", "415"] },
        { "/auth/sign-in", "post", "200", ["400", "401", "403", "415"] },
        { "/auth/sign-out", "post", "204", ["401"] },
        { "/auth/me", "get", "200", ["401"] },
    };

    [Fact]
    public async Task Document_AnswersWithoutSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClientWithoutCookies();

        using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [MemberData(nameof(AccountOperations))]
    public async Task Document_ListsEveryAnswerOfAccountEndpoint(string path, string method, string success, string[] errors)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClientWithoutCookies();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", cancellationToken));

        var responses = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");

        Assert.Equal(
            errors.Append(success).Order(StringComparer.Ordinal),
            responses.EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal));
        if (success != "204")
        {
            var schema = responses.GetProperty(success).GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.EndsWith("/AccountResponse", schema.GetProperty("$ref").GetString(), StringComparison.Ordinal);
        }

        foreach (var error in errors)
        {
            Assert.True(
                responses.GetProperty(error).GetProperty("content").TryGetProperty("application/problem+json", out _),
                $"{method.ToUpperInvariant()} {path} does not describe its {error} answer as problem details.");
        }
    }
}
