using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations when the API starts. The first attempt runs during startup, so with a reachable
/// database the schema is current before the first request. If it fails, the API still starts (and /health
/// reports Unhealthy) while further attempts run in the background until one succeeds.
/// </summary>
/// <remarks>
/// Built on <see cref="BackgroundService"/>, which can be stopped twice and after it was disposed. Test hosts do both:
/// WebApplicationFactory stops the host while the API's own <c>app.Run()</c> stops and disposes it on another thread.
/// </remarks>
internal sealed partial class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    DatabaseMigrationState state,
    ILogger<DatabaseMigrator> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await TryMigrateAsync(cancellationToken))
        {
            // Starts ExecuteAsync, which retries in the background.
            await base.StartAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            do
            {
                await Task.Delay(RetryInterval, stoppingToken);
            }
            while (!await TryMigrateAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task<bool> TryMigrateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>().Database;
            await database.MigrateAsync(cancellationToken);
            state.MarkApplied();
            LogApplied(logger);
            return true;
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, exception, RetryInterval);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations are applied.")]
    private static partial void LogApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Applying database migrations failed; retrying in {RetryInterval}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, TimeSpan retryInterval);
}
