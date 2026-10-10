namespace LogAnalyzer.Core.Contract;

/// <summary>One problem the log shows, with the lines that support it.</summary>
/// <param name="Title">A short headline, at most 120 characters.</param>
/// <param name="LineRefs">The 1-based numbers of the cited lines in the log as submitted.</param>
/// <param name="Evidence">An excerpt, as the model wrote it, that appears on at least one cited line.</param>
/// <param name="Explanation">Why the problem matters and its most likely cause.</param>
/// <param name="SuggestedAction">The next thing an engineer should check or do.</param>
public sealed record Finding(
    string Title,
    Severity Severity,
    FindingCategory Category,
    IReadOnlyList<int> LineRefs,
    string Evidence,
    string Explanation,
    string SuggestedAction);
