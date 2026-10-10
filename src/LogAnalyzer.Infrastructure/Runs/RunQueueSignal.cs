namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// Wakes the worker's loops when a run is queued: RunStore raises it after the submission's commit. The worker also
/// polls, so a missed signal only delays a run.
/// </summary>
public sealed class RunQueueSignal
{
    private TaskCompletionSource _next = NewSource();

    /// <summary>Completes at the next <see cref="Notify"/>.</summary>
    public Task NextSubmission => Volatile.Read(ref _next).Task;

    /// <summary>Wakes every loop that waits for a submission.</summary>
    public void Notify() => Interlocked.Exchange(ref _next, NewSource()).TrySetResult();

    // Continuations run on the thread pool, not on the request that raised the signal.
    private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
