using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LogAnalyzer.IntegrationTests.OpenApi;

/// <summary>
/// The OpenAPI document that the Angular API client is generated from. It is served only in Development,
/// which is the test host's environment.
/// </summary>
public sealed partial class OpenApiDocumentTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // Every answer of each endpoint that docs/accounts-api.md and docs/runs-api.md list: the successes first, with the
    // schema of their body (null for none). A submission answers 202 for a new run and 200 for a reused one.
    public static TheoryData<string, string, string[], string?, string[]> Operations => new()
    {
        { "/auth/register", "post", ["201"], "AccountResponse", ["400", "409", "415"] },
        { "/auth/sign-in", "post", ["200"], "AccountResponse", ["400", "401", "403", "415"] },
        { "/auth/sign-out", "post", ["204"], null, ["401"] },
        { "/auth/me", "get", ["200"], "AccountResponse", ["401"] },
        { "/runs", "post", ["202", "200"], "SubmissionResponse", ["400", "401", "413", "415"] },
        { "/runs", "get", ["200"], "RunPageResponse", ["400", "401"] },
        { "/runs/{id}", "get", ["200"], "RunResponse", ["401", "404"] },
        { "/runs/{id}/lines", "get", ["200"], "RunLinesResponse", ["400", "401", "404"] },
        { "/runs/limits", "get", ["200"], "SubmissionLimitsResponse", ["401"] },
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
        string path, string method, string[] successes, string? successSchema, string[] errors)
    {
        using var document = await ReadDocumentAsync();

        var responses = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");

        Assert.Equal(
            errors.Concat(successes).Order(StringComparer.Ordinal),
            responses.EnumerateObject().Select(response => response.Name).Order(StringComparer.Ordinal));
        foreach (var success in successes.Where(_ => successSchema is not null))
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

    // The web app's messages are typed over this list, so a code missing from it would have no message.
    [Fact]
    public async Task Document_ListsEveryCodeOfErrorTables()
    {
        using var document = await ReadDocumentAsync();

        var problem = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");

        Assert.True(
            problem.GetProperty("properties").TryGetProperty("code", out var code),
            "The ProblemDetails schema has no code.");
        var documented = new[] { "accounts-api.md", "runs-api.md" }
            .SelectMany(name => ErrorTableRow().Matches(File.ReadAllText(Path.Combine(RepositoryRoot, "docs", name))))
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
        Assert.Equal(documented, code.GetProperty("enum").EnumerateArray().Select(value => value.GetString()!).ToHashSet());
    }

    // The web app generates its types from the copy, so an API change must update it. The failure names the command.
    [Fact]
    public async Task Document_EqualsWebCopy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClientWithoutCookies();
        var served = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", cancellationToken));
        var copyPath = Path.Combine(RepositoryRoot, "src", "web", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            // Relaxed escaping keeps media types such as application/problem+json readable.
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            await File.WriteAllTextAsync(copyPath, served!.ToJsonString(options) + "\n", cancellationToken);
            return;
        }

        Assert.True(
            File.Exists(copyPath)
                && JsonNode.DeepEquals(served, JsonNode.Parse(await File.ReadAllTextAsync(copyPath, cancellationToken))),
            "src/web/openapi.json is missing or differs from the served document. To update it, set UPDATE_OPENAPI_SNAPSHOT=1: "
                + "UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/LogAnalyzer.IntegrationTests --filter FullyQualifiedName~OpenApiDocumentTests");
    }

    // The folder that holds LogAnalyzer.slnx, above the test's output folder.
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(directory.FullName, "LogAnalyzer.slnx")))
            {
                directory = directory.Parent
                    ?? throw new InvalidOperationException($"No folder above {AppContext.BaseDirectory} holds LogAnalyzer.slnx.");
            }

            return directory.FullName;
        }
    }

    // A row of an error table, such as "| `EMAIL_TAKEN` | 409 | ... |". The failure reasons table has no status column.
    [GeneratedRegex(@"^\| `([A-Z_]+)` \| \d{3} \|", RegexOptions.Multiline)]
    private static partial Regex ErrorTableRow();

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
