using Microsoft.Extensions.Configuration;

namespace LogAnalyzer.Evals;

/// <summary>
/// What the evals need to call Claude. They run only when RUN_EVALS=1, because they cost money. The key comes from the
/// API's user secrets (<c>Model:Claude:ApiKey</c>) or the ANTHROPIC_API_KEY environment variable, never from a file in
/// the repository.
/// </summary>
public static class EvalSettings
{
    /// <summary>The API project's user secrets id, so the key set for running the API serves the evals too.</summary>
    private const string ApiUserSecretsId = "ea61aa7c-6697-4ca4-8431-087e0ec6b556";

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets(ApiUserSecretsId)
        .AddEnvironmentVariables()
        .Build();

    public static bool Enabled => Environment.GetEnvironmentVariable("RUN_EVALS") == "1";

    /// <summary>The key, or null when neither source has one.</summary>
    public static string? ApiKey =>
        NonBlank(Configuration["Model:Claude:ApiKey"]) ?? NonBlank(Configuration["ANTHROPIC_API_KEY"]);

    /// <summary>The model the evals grade, <c>claude-opus-5-5</c> unless <c>Model:Claude:Model</c> says otherwise.</summary>
    public static string Model => NonBlank(Configuration["Model:Claude:Model"]) ?? "claude-opus-5-5";

    /// <summary>The effort, <c>medium</c> unless <c>Model:Claude:Effort</c> says otherwise.</summary>
    public static string Effort => NonBlank(Configuration["Model:Claude:Effort"]) ?? "medium";

    /// <summary>The model that judges summaries.</summary>
    public const string JudgeModel = "claude-haiku-5-5";

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
