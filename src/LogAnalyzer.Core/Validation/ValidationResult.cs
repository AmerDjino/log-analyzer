using System.Diagnostics.CodeAnalysis;
using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.Core.Validation;

/// <summary>The outcome of validating one response: the accepted analysis, or every reason the response was rejected.</summary>
public sealed class ValidationResult
{
    private ValidationResult(Analysis? analysis, IReadOnlyList<Violation> violations)
    {
        Analysis = analysis;
        Violations = violations;
    }

    /// <summary>The accepted analysis; null when the response was rejected.</summary>
    public Analysis? Analysis { get; }

    /// <summary>Why the response was rejected; empty when it was accepted.</summary>
    public IReadOnlyList<Violation> Violations { get; }

    [MemberNotNullWhen(true, nameof(Analysis))]
    public bool IsAccepted => Analysis is not null;

    internal static ValidationResult Accept(Analysis analysis) => new(analysis, []);

    internal static ValidationResult Reject(IReadOnlyList<Violation> violations) => new(null, violations);
}
