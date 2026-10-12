using System.Diagnostics;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Core.Ingestion;
using LogAnalyzer.Core.Processing;
using LogAnalyzer.Infrastructure.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LogAnalyzer.Evals;

/// <summary>
/// The golden set: each case's log goes through Claude and the pipeline with the validator, as a run does, three times.
/// A case passes when at least two of its runs complete and pass every grader. Runs only with RUN_EVALS=1, because each
/// call costs money; see the README.
/// </summary>
public sealed class LogAnalysisEvals
{
    public const int RunsPerCase = 3;

    public const int PassesNeeded = 2;

    private static readonly string CasesFolder = Path.Combine(AppContext.BaseDirectory, "Cases");

    public static TheoryData<string> Cases =>
    [
        .. Directory.GetFiles(CasesFolder, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(CasesFolder, path).Replace('\\', '/')[..^".json".Length])
            .Order(StringComparer.Ordinal),
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Case_PassesAtTwoOfThreeRuns(string scenario)
    {
        Assert.SkipUnless(EvalSettings.Enabled, "Evals call the real model and cost money. Set RUN_EVALS=1 to run them; see the README.");
        var apiKey = EvalSettings.ApiKey;
        Assert.True(
            apiKey is not null,
            "Model:Claude:ApiKey is not set. Set it in the API's user secrets, or as the ANTHROPIC_API_KEY environment variable; see the README.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var evalCase = EvalCase.Parse(await File.ReadAllTextAsync(Path.Combine(CasesFolder, $"{scenario}.json"), cancellationToken));
        var capability = scenario[..scenario.IndexOf('/', StringComparison.Ordinal)];
        var log = LogText.Read(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "GoldenLogs", capability, evalCase.Log), cancellationToken));
        using var model = CreateModel(apiKey);
        using var judge = new SummaryJudge(apiKey);

        var runs = await Task.WhenAll(
            Enumerable.Range(0, RunsPerCase).Select(_ => RunOnceAsync(model, judge, evalCase, log.Lines, cancellationToken)));

        var reportPath = EvalReport.Record(scenario, runs);
        TestContext.Current.SendDiagnosticMessage($"Eval report: {reportPath}");
        var passes = runs.Count(run => run.Passed);
        Assert.True(
            passes >= PassesNeeded,
            $"{scenario} passed {passes} of {RunsPerCase} runs. {string.Join(" | ", runs.Where(run => !run.Passed).Select(run => run.Failure))}");
    }

    private static ClaudeAnalysisModel CreateModel(string apiKey) => new(
        Options.Create(new ModelOptions
        {
            Provider = ModelProviders.Claude,
            Claude = new ClaudeOptions
            {
                ApiKey = apiKey,
                Model = EvalSettings.Model,
                Effort = EvalSettings.Effort,
                // Long logs take a while; the evals measure quality, not the production timeout.
                Timeout = TimeSpan.FromMinutes(10),
                MaxRetries = 3,
            },
        }),
        new EmbeddedPromptTemplates(),
        TimeProvider.System,
        NullLogger<ClaudeAnalysisModel>.Instance);

    private static async Task<EvalRun> RunOnceAsync(
        IAnalysisModel model, SummaryJudge judge, EvalCase evalCase, string[] lines, CancellationToken cancellationToken)
    {
        var recorder = new MemoryRecorder();
        var started = Stopwatch.GetTimestamp();
        var outcome = await new AnalysisPipeline(model, recorder, TimeProvider.System).ProcessAsync(Guid.CreateVersion7(), lines, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);
        if (!outcome.IsCompleted || recorder.Analysis is not { } analysis)
        {
            return new EvalRun(false, $"The run failed with {outcome.FailureReason}: {outcome.Error?.Message}", recorder.Attempts, elapsed);
        }

        var failures = new List<string>();
        foreach (var grader in evalCase.Graders)
        {
            if (grader.Kind == GraderKinds.Summary)
            {
                var answer = await judge.AskAsync(grader.Question!, analysis.Summary, cancellationToken);
                if (answer != grader.Expected)
                {
                    failures.Add($"The judge answered {answer} to \"{grader.Question}\" for the summary \"{analysis.Summary}\".");
                }
            }
            else if (grader.GradeFindings(analysis) is { } failure)
            {
                failures.Add(failure);
            }
        }

        return new EvalRun(failures.Count == 0, failures.Count == 0 ? null : string.Join(" ", failures), recorder.Attempts, elapsed);
    }

    /// <summary>Keeps what the pipeline records, in memory.</summary>
    private sealed class MemoryRecorder : IRunRecorder
    {
        private readonly List<AttemptRecord> _attempts = [];

        public IReadOnlyList<AttemptRecord> Attempts => _attempts;

        public Analysis? Analysis { get; private set; }

        public Task RecordRejectedAttemptAsync(Guid runId, AttemptRecord attempt, CancellationToken cancellationToken)
        {
            _attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(Guid runId, AttemptRecord acceptedAttempt, Analysis analysis, CancellationToken cancellationToken)
        {
            _attempts.Add(acceptedAttempt);
            Analysis = analysis;
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid runId, string failureReason, AttemptRecord? rejectedAttempt, CancellationToken cancellationToken)
        {
            if (rejectedAttempt is not null)
            {
                _attempts.Add(rejectedAttempt);
            }

            return Task.CompletedTask;
        }
    }
}

/// <summary>One run of a case: whether it passed, why not, its attempts and how long it took.</summary>
public sealed record EvalRun(bool Passed, string? Failure, IReadOnlyList<AttemptRecord> Attempts, TimeSpan Elapsed);
