using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Processing;
using Microsoft.Extensions.Options;
using StopReason = LogAnalyzer.Core.Validation.StopReason;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Analyzes logs with Claude through the official SDK. Each call is one Messages request: the cached system prompt, the
/// cached numbered log and, for a correction, a block that quotes the rejected response. The response format is the
/// output contract, and the validator still checks every reply. The API key goes only to the SDK client.
/// </summary>
public sealed class ClaudeAnalysisModel : IAnalysisModel, IDisposable
{
    private static readonly Dictionary<string, JsonElement> ContractSchema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(AnalysisContract.SchemaJson)!;

    private readonly ClaudeOptions _options;

    private readonly PromptTemplate _template;

    private readonly IAnthropicClient _client;

    public ClaudeAnalysisModel(IOptions<ModelOptions> options, IPromptTemplates templates)
    {
        _options = options.Value.Claude;
        _template = templates.Find(_options.PromptVersion)
            ?? throw new InvalidOperationException($"Model:Claude:PromptVersion '{_options.PromptVersion}' has no prompt templates.");
        IAnthropicClient client = new AnthropicClient
        {
            ApiKey = _options.ApiKey,
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
        var prompt = PromptRenderer.Render(_template, request);
        var response = await _client.WithRawResponse.Messages.Create(Parameters(prompt), cancellationToken);
        var message = await response.Deserialize(cancellationToken);

        var text = string.Concat(message.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));
        var usage = new ModelUsage(
            InputTokens: ToInt(message.Usage.InputTokens + (message.Usage.CacheCreationInputTokens ?? 0) + (message.Usage.CacheReadInputTokens ?? 0)),
            OutputTokens: ToInt(message.Usage.OutputTokens),
            TransportRetries: 0,
            RequestId: response.RequestID);
        return new ModelReply(text, ClaudeStopReasons.Map(message.StopReason?.Raw()), message.Model.Raw(), _options.PromptVersion)
        {
            Usage = usage,
        };
    }

    public void Dispose() => _client.Dispose();

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
