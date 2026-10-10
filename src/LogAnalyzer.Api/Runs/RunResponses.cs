using LogAnalyzer.Infrastructure.Runs;

namespace LogAnalyzer.Api.Runs;

/// <summary>The answer to an accepted submission: the new run's id and its status, queued.</summary>
internal sealed record SubmissionResponse(Guid Id, RunStatus Status);
