namespace LogAnalyzer.Core.Contract;

/// <summary>
/// An accepted analysis of one log: what the log shows and the problems it contains.
/// Mirrors the output contract, schemas/analysis-result.schema.json; docs/output-contract.md describes each field.
/// </summary>
/// <param name="Summary">What the log shows, in at most 1,000 characters.</param>
/// <param name="Findings">The problems found, possibly none.</param>
public sealed record Analysis(string Summary, IReadOnlyList<Finding> Findings);
