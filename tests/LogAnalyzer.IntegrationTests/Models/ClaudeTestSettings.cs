namespace LogAnalyzer.IntegrationTests.Models;

/// <summary>Values the Claude tests configure.</summary>
public static class ClaudeTestSettings
{
    /// <summary>
    /// The API key every test uses. It is not shaped like a real Anthropic key, so secret scanning never takes it for one,
    /// and it is distinctive enough to search for in answers, logs and tables.
    /// </summary>
    public const string ApiKey = "fake-key-for-tests";
}
