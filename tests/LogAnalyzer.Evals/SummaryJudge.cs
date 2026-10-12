using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace LogAnalyzer.Evals;

/// <summary>
/// Answers a yes-or-no question about an analysis' summary with a small model, for the one check code cannot make:
/// whether the summary says no problems were found.
/// </summary>
public sealed class SummaryJudge(string apiKey) : IDisposable
{
    private static readonly Dictionary<string, JsonElement> AnswerSchema =
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {"type":"object","additionalProperties":false,"required":["answer"],
             "properties":{"answer":{"type":"string","enum":["yes","no"]}}}
            """)!;

    private readonly AnthropicClient _client = new() { ApiKey = apiKey };

    public async Task<string> AskAsync(string question, string summary, CancellationToken cancellationToken)
    {
        var message = await _client.Messages.Create(
            new MessageCreateParams
            {
                Model = EvalSettings.JudgeModel,
                MaxTokens = 1_024,
                Messages =
                [
                    new MessageParam
                    {
                        Role = Role.User,
                        Content = $"{question} Answer yes or no.\n\n<summary>\n{summary}\n</summary>",
                    },
                ],
                OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = AnswerSchema } },
            },
            cancellationToken);
        var text = string.Concat(message.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));
        using var answer = JsonDocument.Parse(text);
        return answer.RootElement.GetProperty("answer").GetString()!;
    }

    public void Dispose() => _client.Dispose();
}
