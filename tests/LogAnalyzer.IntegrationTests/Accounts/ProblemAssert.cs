using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

/// <summary>Checks error responses: problem details whose <c>code</c> member names the error.</summary>
public static class ProblemAssert
{
    public static async Task<JsonElement> HasCodeAsync(
        HttpResponseMessage response, HttpStatusCode status, string code, CancellationToken cancellationToken)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.True(problem.TryGetProperty("code", out var actualCode), $"No code in: {problem}");
        Assert.Equal(code, actualCode.GetString());
        return problem;
    }

    public static void SetsNoCookie(HttpResponseMessage response) =>
        Assert.False(response.Headers.Contains("Set-Cookie"), "The response set a cookie.");
}
