using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Types;
using WireMock.Util;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>
/// A local stand-in for the Anthropic Messages API, on a WireMock server. Each <c>POST /v1/messages</c> gets the next
/// scripted answer, in order, and every request is recorded. A request with nothing scripted gets a 400, which no retry
/// repeats, so a call a test did not expect shows as a failed run.
/// </summary>
public sealed class FakeMessagesApi : IDisposable
{
    /// <summary>The usage the canned answers report.</summary>
    public const int InputTokens = 4210;

    public const int OutputTokens = 610;

    private readonly WireMockServer _server;

    private readonly ConcurrentQueue<FakeAnswer> _answers = new();

    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    private int _sequence;

    public FakeMessagesApi()
    {
        _server = WireMockServer.Start();
        _server
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithCallback(AnswerAsync));
    }

    /// <summary>The address to give the SDK as its base URL.</summary>
    public string Url => _server.Url!;

    /// <summary>Every request received, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    /// <summary>Forgets the scripted answers and the recorded requests, so a test starts from nothing.</summary>
    public void Reset()
    {
        _answers.Clear();
        _requests.Clear();
    }

    public void Enqueue(FakeAnswer answer) => _answers.Enqueue(answer);

    /// <summary>The next request is answered with a message holding this text.</summary>
    public void Answer(
        string text,
        string stopReason = "end_turn",
        string model = "claude-opus-5-5",
        TimeSpan delay = default,
        string? requestId = null) =>
        Enqueue(new FakeAnswer(200, Message(text, stopReason, model), Headers(requestId ?? NextRequestId()), delay));

    /// <summary>The next request is answered with an API error, such as 429 <c>rate_limit_error</c>.</summary>
    public void Fail(int status, string errorType, string? retryAfter = null)
    {
        var headers = Headers(NextRequestId());
        if (retryAfter is not null)
        {
            headers["retry-after"] = retryAfter;
        }

        var body = new JsonObject
        {
            ["type"] = "error",
            ["error"] = new JsonObject { ["type"] = errorType, ["message"] = $"Scripted {errorType}." },
        };
        Enqueue(new FakeAnswer(status, body.ToJsonString(), headers, TimeSpan.Zero));
    }

    public void Dispose() => _server.Stop();

    private static string Message(string text, string stopReason, string model)
    {
        var message = new JsonObject
        {
            ["id"] = $"msg_{Guid.NewGuid():N}",
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = model,
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
            ["stop_reason"] = stopReason,
            ["stop_sequence"] = null,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = InputTokens - 1000,
                ["cache_creation_input_tokens"] = 600,
                ["cache_read_input_tokens"] = 400,
                ["output_tokens"] = OutputTokens,
            },
        };
        if (stopReason == "refusal")
        {
            message["stop_details"] = new JsonObject
            {
                ["type"] = "refusal",
                ["category"] = "cyber",
                ["explanation"] = "Scripted refusal.",
            };
        }

        return message.ToJsonString();
    }

    private static Dictionary<string, string> Headers(string requestId) =>
        new(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json", ["request-id"] = requestId };

    private string NextRequestId() => $"req_fake_{Interlocked.Increment(ref _sequence)}";

    private async Task<IResponseMessage> AnswerAsync(IRequestMessage request)
    {
        _requests.Enqueue(new RecordedRequest(
            request.Headers?.ToDictionary(header => header.Key, header => header.Value.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            request.Body ?? string.Empty));
        if (!_answers.TryDequeue(out var answer))
        {
            answer = new FakeAnswer(
                400,
                """{"type":"error","error":{"type":"invalid_request_error","message":"Nothing scripted for this request."}}""",
                Headers(NextRequestId()),
                TimeSpan.Zero);
        }

        if (answer.Delay > TimeSpan.Zero)
        {
            await Task.Delay(answer.Delay);
        }

        return new ResponseMessage
        {
            StatusCode = answer.Status,
            Headers = answer.Headers.ToDictionary(header => header.Key, header => new WireMockList<string>(header.Value)),
            BodyData = new BodyData { BodyAsString = answer.Body, DetectedBodyType = BodyType.String },
        };
    }
}

/// <summary>One scripted answer: its status, body and headers, sent after the delay.</summary>
public sealed record FakeAnswer(int Status, string Body, IReadOnlyDictionary<string, string> Headers, TimeSpan Delay);

/// <summary>A request as the fake received it.</summary>
public sealed record RecordedRequest(IReadOnlyDictionary<string, string> Headers, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}
