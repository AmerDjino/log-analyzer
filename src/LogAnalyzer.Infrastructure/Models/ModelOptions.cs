namespace LogAnalyzer.Infrastructure.Models;

/// <summary>The values <see cref="ModelOptions.Provider"/> takes, in any letter case.</summary>
public static class ModelProviders
{
    /// <summary>The deterministic stand-in model: no network and no key. The default.</summary>
    public const string StandIn = "StandIn";

    /// <summary>Claude, through the Anthropic API. Needs <c>Model:Claude:ApiKey</c>.</summary>
    public const string Claude = "Claude";
}

/// <summary>Settings for the model that analyzes runs: the <c>Model</c> configuration section, checked when the API starts.</summary>
public sealed class ModelOptions
{
    public const string SectionName = "Model";

    /// <summary>Which model analyzes runs: <see cref="ModelProviders.StandIn"/> unless configured.</summary>
    public string Provider { get; set; } = ModelProviders.StandIn;

    /// <summary>Claude's settings, read only when Claude is the provider.</summary>
    public ClaudeOptions Claude { get; set; } = new();

    public bool IsClaude => string.Equals(Provider, ModelProviders.Claude, StringComparison.OrdinalIgnoreCase);

    public bool IsStandIn => string.Equals(Provider, ModelProviders.StandIn, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The <c>Model:Claude</c> settings.</summary>
public sealed class ClaudeOptions
{
    /// <summary>The Anthropic API key. Required for Claude; never logged, returned or stored.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>The prompt templates to use, such as <c>log-analysis/v1</c>: a folder under <c>prompts/</c>.</summary>
    public string PromptVersion { get; set; } = "log-analysis/v1";

    /// <summary>How much the model thinks before it answers: low, medium, high or max.</summary>
    public string Effort { get; set; } = "medium";

    public int MaxOutputTokens { get; set; } = 16_000;

    /// <summary>How long one try of a model call may take before it is abandoned and handled as a transient failure.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>How many times a call is sent again after a transient failure.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>Where the Anthropic API is; empty for the SDK's default. Tests point it at a local fake.</summary>
    public string? BaseUrl { get; set; }
}
