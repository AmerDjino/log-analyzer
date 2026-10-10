using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// One finding of a completed run, as the accepted response gave it, in contract spelling. Written only in the
/// transaction that completes the run.
/// </summary>
public sealed class RunFinding
{
    public Guid RunId { get; set; }

    /// <summary>The finding's index in the accepted response, from 0, which keeps the model's order.</summary>
    public int Position { get; set; }

    public required string Title { get; set; }

    public Severity Severity { get; set; }

    public FindingCategory Category { get; set; }

    public required int[] LineRefs { get; set; }

    public required string Evidence { get; set; }

    public required string Explanation { get; set; }

    public required string SuggestedAction { get; set; }
}
