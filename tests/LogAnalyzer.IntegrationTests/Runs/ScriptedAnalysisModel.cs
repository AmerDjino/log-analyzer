using System.Collections.Concurrent;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Core.Validation;
using Microsoft.Extensions.Time.Testing;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>
/// The model for run tests. Each test scripts the replies, or failures, for its own run, which are given in order. Every
/// request is recorded. A run without a script fails with MODEL_UNAVAILABLE, so a run processed by accident shows.
/// Each call takes one second on the test clock, so attempts have distinct times. Its configured model and prompt
/// version, which reuse compares, can be changed to act as a configuration change.
/// </summary>
public sealed class ScriptedAnalysisModel(FakeTimeProvider clock) : IAnalysisModel
{
    public const string Model = "scripted";

    public const string PromptVersion = "scripted-v1";

    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<Func<ModelReply>>> _scripts = new();

    private readonly ConcurrentQueue<AnalysisRequest> _requests = new();

    public string ConfiguredModel { get; set; } = Model;

    public string ConfiguredPromptVersion { get; set; } = PromptVersion;

    /// <summary>Every request received, in order, for any run.</summary>
    public IReadOnlyList<AnalysisRequest> Requests => [.. _requests];

    public IReadOnlyList<AnalysisRequest> RequestsFor(Guid runId) => [.. _requests.Where(request => request.RunId == runId)];

    /// <summary>The run's next model call answers with this text.</summary>
    public void Reply(Guid runId, string text, StopReason stopReason = StopReason.Completed, ModelUsage? usage = null) =>
        Script(runId).Enqueue(() =>
            new ModelReply(text, stopReason, ConfiguredModel, ConfiguredPromptVersion) { Usage = usage ?? ModelUsage.None });

    /// <summary>The run's next model call yields no response.</summary>
    public void Fail(Guid runId, string failureReason) =>
        Script(runId).Enqueue(() => throw new ModelCallException(failureReason, $"Scripted {failureReason}."));

    public Task<ModelReply> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);
        clock.Advance(TimeSpan.FromSeconds(1));
        if (!_scripts.TryGetValue(request.RunId, out var script) || !script.TryDequeue(out var next))
        {
            throw new ModelCallException(FailureReasons.ModelUnavailable, $"Nothing scripted for run {request.RunId}.");
        }

        return Task.FromResult(next());
    }

    private ConcurrentQueue<Func<ModelReply>> Script(Guid runId) => _scripts.GetOrAdd(runId, _ => new());
}
