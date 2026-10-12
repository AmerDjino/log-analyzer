using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Models;

/// <summary>
/// Checks the model settings when the API starts. Claude's settings are checked only when Claude is the provider, since
/// the stand-in never reads them. Each message names its setting and never quotes the key.
/// </summary>
public sealed class ModelOptionsValidation(IPromptTemplates templates) : IValidateOptions<ModelOptions>
{
    public const int MaxOutputTokensLimit = 128_000;

    public const int MaxRetriesLimit = 10;

    public static readonly TimeSpan TimeoutLimit = TimeSpan.FromMinutes(10);

    /// <summary>The efforts the setting accepts, in any letter case.</summary>
    public static readonly string[] Efforts = ["low", "medium", "high", "max"];

    public ValidateOptionsResult Validate(string? name, ModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsStandIn && !options.IsClaude)
        {
            return ValidateOptionsResult.Fail(
                $"Model:Provider must be {ModelProviders.StandIn} or {ModelProviders.Claude}, not '{options.Provider}'.");
        }

        return options.IsClaude ? ValidateClaude(options.Claude) : ValidateOptionsResult.Success;
    }

    private ValidateOptionsResult ValidateClaude(ClaudeOptions claude)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(claude.ApiKey))
        {
            failures.Add(
                "Model:Claude:ApiKey is missing, and Model:Provider is Claude. Set it as the Docker secret, in user secrets "
                + "or as Model__Claude__ApiKey (see README).");
        }

        if (string.IsNullOrWhiteSpace(claude.Model))
        {
            failures.Add("Model:Claude:Model must name a model, such as claude-opus-5-5.");
        }

        if (templates.Find(claude.PromptVersion) is null)
        {
            failures.Add($"Model:Claude:PromptVersion '{claude.PromptVersion}' has no prompt templates under prompts/.");
        }

        if (!Efforts.Contains(claude.Effort, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"Model:Claude:Effort must be one of {string.Join(", ", Efforts)}.");
        }

        if (claude.MaxOutputTokens is < 1 or > MaxOutputTokensLimit)
        {
            failures.Add($"Model:Claude:MaxOutputTokens must be between 1 and {MaxOutputTokensLimit}.");
        }

        if (claude.Timeout <= TimeSpan.Zero || claude.Timeout > TimeoutLimit)
        {
            failures.Add($"Model:Claude:Timeout must be more than 0 and at most {TimeoutLimit}, written as hh:mm:ss.");
        }

        if (claude.MaxRetries is < 0 or > MaxRetriesLimit)
        {
            failures.Add($"Model:Claude:MaxRetries must be between 0 and {MaxRetriesLimit}.");
        }

        if (!string.IsNullOrEmpty(claude.BaseUrl)
            && !(Uri.TryCreate(claude.BaseUrl, UriKind.Absolute, out var baseUrl) && baseUrl.Scheme is "http" or "https"))
        {
            failures.Add("Model:Claude:BaseUrl must be an absolute http or https address, or empty for the default.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
