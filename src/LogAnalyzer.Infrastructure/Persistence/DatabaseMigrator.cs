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
internal sealed partial class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    DatabaseMigrationState state,
    ILogger<DatabaseMigrator> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _stopping = new();
    private Task? _retries;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!await TryMigrateAsync(cancellationToken))
        {
            _retries = RetryUntilAppliedAsync(_stopping.Token);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_retries is not null)
        {
            await _retries.WaitAsync(cancellationToken);
        }
    }

    public void Dispose() => _stopping.Dispose();

    private async Task RetryUntilAppliedAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (await TryMigrateAsync(stoppingToken))
            {
                return;
            }
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
        catch (Exception exception) when (exception is not OperationCanceledException)
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
