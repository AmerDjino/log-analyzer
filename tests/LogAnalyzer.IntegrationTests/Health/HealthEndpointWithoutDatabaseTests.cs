using System.Net;

namespace LogAnalyzer.IntegrationTests.Health;

public sealed class HealthEndpointWithoutDatabaseTests(UnreachableDatabaseApiFactory factory)
    : IClassFixture<UnreachableDatabaseApiFactory>
{
    [Fact]
    public async Task Health_ReturnsServiceUnavailable_WhenDatabaseIsUnreachable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
