using LogAnalyzer.Infrastructure.Models;

namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>The model settings' rules, checked without starting the API.</summary>
public sealed class ModelOptionsValidationTests
{
    private readonly ModelOptionsValidation _validation = new(new FixedPromptTemplates("log-analysis/v1"));

    [Fact]
    public void Defaults_AreTheStandInAndClaudeOpusAtMediumEffort()
    {
        var options = new ModelOptions();

        Assert.Equal(ModelProviders.StandIn, options.Provider);
        Assert.Equal("claude-opus-5-5", options.Claude.Model);
        Assert.Equal("log-analysis/v1", options.Claude.PromptVersion);
        Assert.Equal("medium", options.Claude.Effort);
        Assert.Equal(16_000, options.Claude.MaxOutputTokens);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Claude.Timeout);
        Assert.Equal(2, options.Claude.MaxRetries);
        Assert.Null(options.Claude.BaseUrl);
    }

    [Fact]
    public void Validate_AcceptsTheStandInWithoutKey()
    {
        Assert.True(_validation.Validate(null, new ModelOptions()).Succeeded);
    }

    [Theory]
    [InlineData("standin")]
    [InlineData("claude")]
    public void Validate_AcceptsProviderInAnyLetterCase(string provider)
    {
        var options = new ModelOptions { Provider = provider, Claude = { ApiKey = ClaudeTestSettings.ApiKey } };

        Assert.True(_validation.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("gpt")]
    [InlineData("")]
    public void Validate_RefusesUnknownProvider(string provider)
    {
        var result = _validation.Validate(null, new ModelOptions { Provider = provider });

        Assert.True(result.Failed);
        Assert.Contains("Model:Provider", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RefusesClaudeWithoutKey(string? apiKey)
    {
        var result = _validation.Validate(null, Claude(options => options.ApiKey = apiKey));

        Assert.True(result.Failed);
        Assert.Contains("Model:Claude:ApiKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsClaudeWithKeyAndDefaults()
    {
        Assert.True(_validation.Validate(null, Claude(_ => { })).Succeeded);
    }

    [Fact]
    public void Validate_RefusesPromptVersionWithoutTemplates()
    {
        var result = _validation.Validate(null, Claude(options => options.PromptVersion = "log-analysis/v9"));

        Assert.True(result.Failed);
        Assert.Contains("Model:Claude:PromptVersion", result.FailureMessage, StringComparison.Ordinal);
    }

    public static TheoryData<string, Action<ClaudeOptions>> SettingsOutsideTheirRange => new()
    {
        { "Model:Claude:Model", options => options.Model = " " },
        { "Model:Claude:Effort", options => options.Effort = "extreme" },
        { "Model:Claude:MaxOutputTokens", options => options.MaxOutputTokens = 0 },
        { "Model:Claude:MaxOutputTokens", options => options.MaxOutputTokens = 128_001 },
        { "Model:Claude:Timeout", options => options.Timeout = TimeSpan.Zero },
        { "Model:Claude:Timeout", options => options.Timeout = TimeSpan.FromMinutes(11) },
        { "Model:Claude:MaxRetries", options => options.MaxRetries = -1 },
        { "Model:Claude:MaxRetries", options => options.MaxRetries = 11 },
        { "Model:Claude:BaseUrl", options => options.BaseUrl = "/v1" },
        { "Model:Claude:BaseUrl", options => options.BaseUrl = "ftp://example.com" },
    };

    [Theory]
    [MemberData(nameof(SettingsOutsideTheirRange))]
    public void Validate_RefusesClaudeSettingOutsideItsRange(string setting, Action<ClaudeOptions> change)
    {
        var result = _validation.Validate(null, Claude(change));

        Assert.True(result.Failed);
        Assert.Contains(setting, result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(ClaudeTestSettings.ApiKey, result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("Medium")]
    [InlineData("high")]
    [InlineData("max")]
    public void Validate_AcceptsEachEffort(string effort)
    {
        Assert.True(_validation.Validate(null, Claude(options => options.Effort = effort)).Succeeded);
    }

    // With the stand-in, Claude's settings are not read, so they cannot stop the API.
    [Fact]
    public void Validate_IgnoresClaudeSettingsForTheStandIn()
    {
        var options = new ModelOptions { Claude = { PromptVersion = "log-analysis/v9", MaxRetries = -1 } };

        Assert.True(_validation.Validate(null, options).Succeeded);
    }

    private static ModelOptions Claude(Action<ClaudeOptions> change)
    {
        var options = new ModelOptions { Provider = ModelProviders.Claude, Claude = { ApiKey = ClaudeTestSettings.ApiKey } };
        change(options.Claude);
        return options;
    }
}

/// <summary>Prompt templates for the given versions, each with a short system and correction text.</summary>
public sealed class FixedPromptTemplates(params string[] versions) : IPromptTemplates
{
    public PromptTemplate? Find(string version) =>
        versions.Contains(version, StringComparer.Ordinal)
            ? new PromptTemplate(version, $"System prompt {version}.", $"Correction prompt {version}.")
            : null;
}
