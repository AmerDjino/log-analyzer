using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The size limit a client reads before it sends a file. <see cref="RunsApiFactory"/> sets the limit to 1 MB, and
/// <see cref="PostgresApiFactory"/> leaves the setting out.
/// </summary>
public sealed class SubmissionLimitsTests(RunsApiFactory factory, PostgresApiFactory defaultLimitFactory)
    : IClassFixture<RunsApiFactory>, IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "log-ingestion/configured-size-limit-is-reported")]
    public async Task ReadLimits_ReportsConfiguredSizeLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(factory, cancellationToken);

        using var response = await runs.ReadLimitsAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var limits = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(RunsApiFactory.MaxSubmissionBytes, limits.GetProperty("maxSubmissionBytes").GetInt32());
    }

    // Without the setting, the API reports the default it enforces, 10 MB.
    [Fact]
    public async Task ReadLimits_ReportsDefaultSizeLimitWithoutSetting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (runs, _) = await RunsClient.SignInNewPersonAsync(defaultLimitFactory, cancellationToken);

        using var response = await runs.ReadLimitsAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var limits = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(10_485_760, limits.GetProperty("maxSubmissionBytes").GetInt32());
    }
}
