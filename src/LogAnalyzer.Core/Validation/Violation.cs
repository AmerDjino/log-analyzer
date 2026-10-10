namespace LogAnalyzer.Core.Validation;

/// <summary>One reason a response was rejected.</summary>
/// <param name="Code">One of the <see cref="ReasonCodes"/>, such as LINE_OUT_OF_RANGE.</param>
/// <param name="Path">Where in the response, such as <c>findings[0].line_refs[0]</c>; empty for the response as a whole.</param>
/// <param name="Message">A sentence for people, and for the corrective request to the model.</param>
public sealed record Violation(string Code, string Path, string Message);
