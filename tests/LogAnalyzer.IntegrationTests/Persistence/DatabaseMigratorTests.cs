using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LogAnalyzer.IntegrationTests.Persistence;

public sealed class DatabaseMigratorTests(UnreachableDatabaseApiFactory factory) : IClassFixture<UnreachableDatabaseApiFactory>
{
    // When a test host shuts down, WebApplicationFactory stops the host while the API's own app.Run() stops and
    // disposes it on another thread, so the migrator can be stopped after it was disposed, and more than once.
    [Fact]
    public async Task Stop_AfterDispose_DoesNotThrow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Without a database the first attempt fails, so the background retries are running.
        var migrator = factory.Services.GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "DatabaseMigrator");

        ((IDisposable)migrator).Dispose();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await migrator.StopAsync(cancellationToken);
            await migrator.StopAsync(cancellationToken);
        });

        Assert.Null(exception);
    }
}
