using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LogAnalyzer.IntegrationTests.Accounts;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// Calls the run endpoints for one signed-in person. A log is the raw request body, the way the web shell sends a paste
/// (its text) and an upload (the file's bytes).
/// </summary>
public sealed class RunsClient(HttpClient client)
{
    public HttpClient Http => client;

    /// <summary>Registers a new account and signs in with it, so the client's runs belong to a person no other test uses.</summary>
    public static async Task<(RunsClient Runs, AccountResponse Person)> SignInNewPersonAsync(
        PostgresApiFactory factory, CancellationToken cancellationToken)
    {
        var accounts = new AccountsClient(factory.CreateClient());
        var person = await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);
        return (new RunsClient(accounts.Http), person);
    }

    /// <summary>A body of the given bytes and Content-Type, sent as is.</summary>
    public static ByteArrayContent Body(byte[] content, string contentType)
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return body;
    }

    /// <summary>Submits pasted text as text/plain; charset=utf-8, without a file name.</summary>
    public Task<HttpResponseMessage> PasteAsync(string text, CancellationToken cancellationToken) =>
        SubmitAsync(new StringContent(text, Encoding.UTF8, "text/plain"), fileName: null, cancellationToken);

    /// <summary>Submits a file's bytes as text/plain without a charset, naming the file.</summary>
    public Task<HttpResponseMessage> UploadAsync(string fileName, byte[] content, CancellationToken cancellationToken) =>
        SubmitAsync(Body(content, "text/plain"), fileName, cancellationToken);

    /// <summary>Submits any body, for tests that choose its Content-Type or how it is sent.</summary>
    public async Task<HttpResponseMessage> SubmitAsync(HttpContent body, string? fileName, CancellationToken cancellationToken)
    {
        var path = fileName is null ? "/runs" : $"/runs?fileName={Uri.EscapeDataString(fileName)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body };
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Checks that a submission was accepted: 202 with status queued and no Location header, which would point past nginx.
    /// Returns the new run's id.
    /// </summary>
    public static async Task<Guid> AcceptedRunIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var submission = await response.Content.ReadFromJsonAsync<SubmissionResponse>(cancellationToken);
        Assert.NotNull(submission);
        Assert.NotEqual(Guid.Empty, submission.Id);
        Assert.Equal("queued", submission.Status);
        return submission.Id;
    }
}

public sealed record SubmissionResponse(Guid Id, string Status);
