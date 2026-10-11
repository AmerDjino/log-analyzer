using System.Net;
using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.OpenApi;

/// <summary>
/// The OpenAPI document that the Angular API client is generated from. It is served only in Development,
/// which is the test host's environment.
/// </summary>
public sealed class OpenApiDocumentTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // Every answer of each endpoint that docs/accounts-api.md and docs/runs-api.md list, the success first, with the
    // schema of the success body (null for none).
    public static TheoryData<string, string, string, string?, string[]> Operations => new()
    {
        { "/auth/register", "post", "201", "AccountResponse", ["400", "409", "415"] },
        { "/auth/sign-in", "post", "200", "AccountResponse", ["400", "401", "403", "415"] },
        { "/auth/sign-out", "post", "204", null, ["401"] },
        { "/auth/me", "get", "200", "AccountResponse", ["401"] },
        { "/runs", "post", "202", "SubmissionResponse", ["400", "401", "413", "415"] },
        { "/runs", "get", "200", "RunPageResponse", ["400", "401"] },
        { "/runs/{id}", "get", "200", "RunResponse", ["401", "404"] },
        { "/runs/{id}/lines", "get", "200", "RunLinesResponse", ["400", "401", "404"] },
        { "/runs/limits", "get", "200", "SubmissionLimitsResponse", ["401"] },
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
    [MemberData(nameof(Operations))]
    public async Task Document_ListsEveryAnswerOfEndpoint(
        string path, string method, string success, string? successSchema, string[] errors)
    {
        using var document = await ReadDocumentAsync();

        var responses = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");

        Assert.Equal(
            errors.Append(success).Order(StringComparer.Ordinal),
            responses.EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal));
        if (successSchema is not null)
        {
            var schema = responses.GetProperty(success).GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.EndsWith($"/{successSchema}", schema.GetProperty("$ref").GetString(), StringComparison.Ordinal);
        }

        foreach (var error in errors)
        {
            Assert.True(
                responses.GetProperty(error).GetProperty("content").TryGetProperty("application/problem+json", out _),
                $"{method.ToUpperInvariant()} {path} does not describe its {error} answer as problem details.");
        }
    }

    // Described as binary, so a generated client sends a file's bytes unchanged instead of text it decoded first.
    [Fact]
    public async Task Document_DescribesSubmittedLogAsBinaryText()
    {
        using var document = await ReadDocumentAsync();

        var content = document.RootElement.GetProperty("paths").GetProperty("/runs").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content");

        Assert.Equal(["text/plain"], content.EnumerateObject().Select(mediaType => mediaType.Name));
        var schema = Resolve(document, content.GetProperty("text/plain").GetProperty("schema"));
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal("binary", schema.GetProperty("format").GetString());
    }

    private async Task<JsonDocument> ReadDocumentAsync()
    {
        using var client = factory.CreateClientWithoutCookies();
        return JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
    }

    // Follows a "#/components/schemas/Name" reference, if the schema is one.
    private static JsonElement Resolve(JsonDocument document, JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? document.RootElement.GetProperty("components").GetProperty("schemas")
                .GetProperty(reference.GetString()!["#/components/schemas/".Length..])
            : schema;
}
