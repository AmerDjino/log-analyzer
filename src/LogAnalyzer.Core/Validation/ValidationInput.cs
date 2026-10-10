namespace LogAnalyzer.Core.Validation;

/// <summary>A model response and what it is checked against.</summary>
/// <param name="ResponseText">The response text exactly as the model service returned it.</param>
/// <param name="StopReason">Why the model stopped, as the model service reports it.</param>
/// <param name="LogLines">The submitted log's lines in order, without line breaks: line N is <c>LogLines[N - 1]</c>.</param>
public sealed record ValidationInput(string ResponseText, StopReason StopReason, IReadOnlyList<string> LogLines);
