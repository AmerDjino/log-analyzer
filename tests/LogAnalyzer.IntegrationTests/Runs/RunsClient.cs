using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    /// <summary>Submits a log that must be accepted, as an upload when it has a file name, and returns the new run's id.</summary>
    public async Task<Guid> SubmitAcceptedAsync(string log, string? fileName, CancellationToken cancellationToken)
    {
        using var response = fileName is null
            ? await PasteAsync(log, cancellationToken)
            : await UploadAsync(fileName, Encoding.UTF8.GetBytes(log), cancellationToken);
        return await AcceptedRunIdAsync(response, cancellationToken);
    }

    public Task<HttpResponseMessage> ReadAsync(string id, CancellationToken cancellationToken) =>
        client.GetAsync($"/runs/{Uri.EscapeDataString(id)}", cancellationToken);

    /// <summary>Reads a run that must be found, as the JSON the API returns.</summary>
    public async Task<JsonElement> ReadRunAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await ReadAsync(id.ToString(), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>
    /// Reads a run until it has completed or failed, for tests with a real worker, and fails the test after 30 seconds.
    /// <paramref name="beforeEachRead"/> runs before every read, for example to move a test clock.
    /// </summary>
    public async Task<JsonElement> ReadWhenFinishedAsync(
        Guid id, CancellationToken cancellationToken, Action? beforeEachRead = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            beforeEachRead?.Invoke();
            var run = await ReadRunAsync(id, cancellationToken);
            var status = run.GetProperty("status").GetString();
            if (status is "completed" or "failed")
            {
                return run;
            }

            Assert.True(DateTime.UtcNow < deadline, $"Run {id} is still {status} after 30 seconds.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    /// <summary>Lists runs; <paramref name="limit"/> and <paramref name="cursor"/> are sent as given, when not null.</summary>
    public Task<HttpResponseMessage> ListAsync(CancellationToken cancellationToken, string? limit = null, string? cursor = null)
    {
        var query = new List<string>();
        if (limit is not null)
        {
            query.Add($"limit={Uri.EscapeDataString(limit)}");
        }

        if (cursor is not null)
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        return client.GetAsync(query.Count == 0 ? "/runs" : $"/runs?{string.Join('&', query)}", cancellationToken);
    }

    /// <summary>Lists a page of runs that must be answered, as the JSON the API returns.</summary>
    public async Task<JsonElement> ListPageAsync(CancellationToken cancellationToken, string? limit = null, string? cursor = null)
    {
        using var response = await ListAsync(cancellationToken, limit, cursor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>The ids of a page's runs, in the order listed.</summary>
    public static List<Guid> IdsOf(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

    /// <summary>
    /// Reads a range of a run's lines; <paramref name="from"/> and <paramref name="to"/> are sent as given, when not null.
    /// </summary>
    public Task<HttpResponseMessage> ReadLinesAsync(string id, string? from, string? to, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (from is not null)
        {
            query.Add($"from={Uri.EscapeDataString(from)}");
        }

        if (to is not null)
        {
            query.Add($"to={Uri.EscapeDataString(to)}");
        }

        var path = $"/runs/{Uri.EscapeDataString(id)}/lines";
        return client.GetAsync(query.Count == 0 ? path : $"{path}?{string.Join('&', query)}", cancellationToken);
    }

    public Task<HttpResponseMessage> ReadLimitsAsync(CancellationToken cancellationToken) =>
        client.GetAsync("/runs/limits", cancellationToken);

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
