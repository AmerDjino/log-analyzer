using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StopReason = LogAnalyzer.Core.Validation.StopReason;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Analyzes logs with Claude through the official SDK. Each call is one Messages request: the cached system prompt, the
/// cached numbered log and, for a correction, a block that quotes the rejected response. The response format is the
/// output contract, and the validator still checks every reply. The API key goes only to the SDK client.
/// <para>
/// The adapter, not the SDK, retries and times out each try, so it can count retries: see <see cref="ClaudeRetryPolicy"/>.
/// A call that cannot get an answer throws <see cref="ModelCallException"/>, and cancellation by the host passes through.
/// </para>
/// </summary>
public sealed partial class ClaudeAnalysisModel : IAnalysisModel, IDisposable
{
    private static readonly Dictionary<string, JsonElement> ContractSchema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(AnalysisContract.SchemaJson)!;

    private readonly ClaudeOptions _options;

    private readonly PromptTemplate _template;

    private readonly HttpClient _httpClient = new(new RetryAfterHandler()) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    private readonly IAnthropicClient _client;

    private readonly TimeProvider _timeProvider;

    private readonly ILogger<ClaudeAnalysisModel> _logger;

    public ClaudeAnalysisModel(
        IOptions<ModelOptions> options,
        IPromptTemplates templates,
        TimeProvider timeProvider,
        ILogger<ClaudeAnalysisModel> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        _options = options.Value.Claude;
        _template = templates.Find(_options.PromptVersion)
            ?? throw new InvalidOperationException($"Model:Claude:PromptVersion '{_options.PromptVersion}' has no prompt templates.");
        IAnthropicClient client = new AnthropicClient
        {
            ApiKey = _options.ApiKey,
            HttpClient = _httpClient,
            // The adapter retries and times out each try itself, so it can count retries; the SDK's own timeout only
            // backs it up.
            MaxRetries = 0,
            Timeout = _options.Timeout + TimeSpan.FromSeconds(30),
        };
        _client = string.IsNullOrEmpty(_options.BaseUrl)
            ? client
            : client.WithOptions(clientOptions => clientOptions with { BaseUrl = _options.BaseUrl });
    }

    public string ConfiguredModel => _options.Model;

    public string ConfiguredPromptVersion => _options.PromptVersion;

    public async Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        var parameters = Parameters(PromptRenderer.Render(_template, request));
        for (var retries = 0; ; retries++)
        {
            TimeSpan? retryAfter = null;
            Exception failure;
            var retryAfterHeader = RetryAfterHandler.Track();
            using var timeout = new CancellationTokenSource(_options.Timeout, _timeProvider);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                return await CallAsync(parameters, retries, attempt.Token);
            }
            catch (Exception exception) when (cancellationToken.IsCancellationRequested)
            {
                // The host is stopping: the pipeline fails the run as interrupted.
                throw new OperationCanceledException("The model call was cancelled because the API is stopping.", exception, cancellationToken);
            }
            catch (Exception exception) when (timeout.IsCancellationRequested)
            {
                failure = new TimeoutException($"The model service did not answer within {_options.Timeout}.", exception);
            }
            catch (AnthropicApiException exception) when (!ClaudeRetryPolicy.IsTransient((int)exception.StatusCode))
            {
                throw new ModelCallException(
                    FailureReasons.ModelConfigurationError,
                    $"The model service refused the request with status {(int)exception.StatusCode}; retrying cannot fix that.",
                    exception);
            }
            catch (AnthropicApiException exception)
            {
                failure = exception;
                retryAfter = ClaudeRetryPolicy.ParseRetryAfter(retryAfterHeader.Value);
            }
            catch (Exception exception) when (exception is AnthropicIOException or HttpRequestException)
            {
                failure = exception;
            }

            if (retries == _options.MaxRetries)
            {
                throw new ModelCallException(
                    FailureReasons.ModelUnavailable,
                    $"The model service failed {retries + 1} times in a row: {failure.Message}",
                    failure);
            }

            var delay = ClaudeRetryPolicy.Delay(retries + 1, retryAfter, Random.Shared.NextDouble());
            LogRetry(_logger, failure, retries + 1, delay);
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _httpClient.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Model call failed; retry {Retry} follows in {Delay}.")]
    private static partial void LogRetry(ILogger logger, Exception exception, int retry, TimeSpan delay);

    private async Task<ModelReply> CallAsync(MessageCreateParams parameters, int retries, CancellationToken cancellationToken)
    {
        var response = await _client.WithRawResponse.Messages.Create(parameters, cancellationToken);
        var message = await response.Deserialize(cancellationToken);

        var text = string.Concat(message.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));
        var usage = new ModelUsage(
            InputTokens: ToInt(message.Usage.InputTokens + (message.Usage.CacheCreationInputTokens ?? 0) + (message.Usage.CacheReadInputTokens ?? 0)),
            OutputTokens: ToInt(message.Usage.OutputTokens),
            TransportRetries: retries,
            RequestId: response.RequestID);
        return new ModelReply(text, ClaudeStopReasons.Map(message.StopReason?.Raw()), message.Model.Raw(), _options.PromptVersion)
        {
            Usage = usage,
        };
    }

    private static int ToInt(long tokens) => (int)Math.Clamp(tokens, 0, int.MaxValue);

    private static Effort ToEffort(string effort) => effort.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    private MessageCreateParams Parameters(RenderedPrompt prompt)
    {
        var content = new List<ContentBlockParam>
        {
            new TextBlockParam { Text = prompt.Log, CacheControl = new CacheControlEphemeral() },
        };
        if (prompt.Correction is { } correction)
        {
            content.Add(new TextBlockParam { Text = correction });
        }

        return new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxOutputTokens,
            System = new List<TextBlockParam> { new() { Text = prompt.System, CacheControl = new CacheControlEphemeral() } },
            Messages = [new MessageParam { Role = Role.User, Content = content }],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = ContractSchema },
                Effort = ToEffort(_options.Effort),
            },
        };
    }
}
