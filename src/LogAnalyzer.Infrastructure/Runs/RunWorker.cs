using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// Processes queued runs in the background. Once the migrations are applied, it runs
/// <see cref="AnalysisOptions.MaxConcurrentRuns"/> loops. Each loop claims and processes runs until none is queued,
/// then waits for whichever comes first: the signal a submission raises after its commit, or the poll interval.
/// </summary>
/// <remarks>
/// An exception in a loop is logged, and the loop waits the same way before it tries again: an exception that escaped
/// would stop the host, and the API must keep answering /health while PostgreSQL is down. Runs one instance per
/// database; see docs/runs-api.md.
/// </remarks>
internal sealed partial class RunWorker(
    RunProcessor processor,
    RunQueueSignal signal,
    DatabaseMigrationState migrations,
    IOptions<AnalysisOptions> options,
    TimeProvider timeProvider,
    ILogger<RunWorker> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await migrations.WhenApplied.WaitAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        await Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrentRuns).Select(_ => ProcessRunsAsync(stoppingToken)));
    }

    private async Task ProcessRunsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Taken before the claim, so a run submitted after the queue was found empty still wakes this loop.
            var submitted = signal.NextSubmission;
            try
            {
                if (await processor.ProcessNextAsync(stoppingToken))
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogFailed(logger, exception, PollInterval);
            }

            // A cancelled delay ends the wait without throwing, and the loop then stops.
            await Task.WhenAny(submitted, Task.Delay(PollInterval, timeProvider, stoppingToken));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing runs failed; trying again in {RetryInterval}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, TimeSpan retryInterval);
}
