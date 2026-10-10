using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using LogAnalyzer.Core.Contract;

namespace LogAnalyzer.Core.Validation;

/// <summary>
/// Checks a model response against the output contract and the submitted log before any part of it is used.
/// A pure function: no network, database or clock.
/// </summary>
public static class ResponseValidator
{
    private const int MaxSummaryLength = 1000;

    private const int MaxTitleLength = 120;

    private static readonly ContractSchema Schema = ContractSchema.Load(AnalysisContract.SchemaJson);

    // Exactly one JSON document: no comments, no trailing commas and no repeated field names.
    private static readonly JsonDocumentOptions StrictJson = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowDuplicateProperties = false,
    };

    public static ValidationResult Validate(ValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.ResponseText);
        ArgumentNullException.ThrowIfNull(input.LogLines);

        // An incomplete response is rejected without reading its text.
        switch (input.StopReason)
        {
            case StopReason.OutputLimit:
                return Reject(ReasonCodes.Truncated, "The model stopped at its output limit, so the response is cut off.");
            case StopReason.Refusal:
                return Reject(ReasonCodes.Refused, "The model declined to answer.");
        }

        if (ParseObject(input.ResponseText) is not { } response)
        {
            return Reject(
                ReasonCodes.NotJson, "The response must be exactly one JSON object, with no code fence or other text around it.");
        }

        // Every violation is collected, rather than stopping at the first, so one corrective request can name them all.
        var violations = new List<Violation>();
        Schema.Check(response, violations);
        CheckCitations(response, input.LogLines, violations);
        CheckTextLimits(response, violations);

        return violations.Count == 0 ? ValidationResult.Accept(ToAnalysis(response)) : ValidationResult.Reject(violations);
    }

    // Limits the schema cannot express: no blank text, and a maximum length for the summary and each title.
    private static void CheckTextLimits(JsonObject response, List<Violation> violations)
    {
        CheckText(response, "summary", ResponsePath.Root, MaxSummaryLength, violations);
        foreach (var (finding, path) in Findings(response))
        {
            CheckText(finding, "title", path, MaxTitleLength, violations);
            CheckText(finding, "evidence", path, maxLength: null, violations);
            CheckText(finding, "explanation", path, maxLength: null, violations);
            CheckText(finding, "suggested_action", path, maxLength: null, violations);
        }
    }

    private static void CheckText(JsonObject parent, string field, string parentPath, int? maxLength, List<Violation> violations)
    {
        if ((parent[field] as JsonValue)?.TryGetValue(out string? text) != true)
        {
            return;
        }

        var path = ResponsePath.Field(parentPath, field);
        if (string.IsNullOrWhiteSpace(text))
        {
            violations.Add(new Violation(ReasonCodes.BlankField, path, "The field is empty or contains only whitespace."));
            return;
        }

        // Unicode code points, so an emoji counts as one character although a .NET string stores it as two UTF-16 units.
        var length = text.EnumerateRunes().Count();
        if (length > maxLength)
        {
            violations.Add(new Violation(
                ReasonCodes.FieldTooLong,
                path,
                string.Create(CultureInfo.InvariantCulture, $"The field has {length} characters; at most {maxLength} are allowed.")));
        }
    }

    // Every cited line must be a line of the log, and the evidence must appear on one of the cited lines.
    // Values with the wrong type are skipped here: the schema check has already reported them.
    private static void CheckCitations(JsonObject response, IReadOnlyList<string> logLines, List<Violation> violations)
    {
        foreach (var (finding, path) in Findings(response))
        {
            var citedLines = new List<string>();
            if (finding["line_refs"] is JsonArray lineRefs)
            {
                var lineRefsPath = ResponsePath.Field(path, "line_refs");
                for (var index = 0; index < lineRefs.Count; index++)
                {
                    if (!ContractSchema.TryGetInteger(lineRefs[index], out var number))
                    {
                        continue;
                    }

                    if (number >= 1 && number <= logLines.Count)
                    {
                        citedLines.Add(logLines[(int)number - 1]);
                    }
                    else
                    {
                        violations.Add(new Violation(
                            ReasonCodes.LineOutOfRange, ResponsePath.Item(lineRefsPath, index), OutOfRangeMessage(number, logLines.Count)));
                    }
                }
            }

            // Evidence can only be checked against lines that exist. Blank evidence is the field limits' concern.
            var evidence = (finding["evidence"] as JsonValue)?.TryGetValue(out string? text) == true ? text.Trim() : string.Empty;
            if (citedLines.Count > 0 && evidence.Length > 0
                && !citedLines.Any(line => line.Contains(evidence, StringComparison.Ordinal)))
            {
                violations.Add(new Violation(
                    ReasonCodes.EvidenceNotFound,
                    ResponsePath.Field(path, "evidence"),
                    "The evidence does not appear on any cited line. Copy it character for character from one of them."));
            }
        }
    }

    private static string OutOfRangeMessage(decimal line, int lineCount) => lineCount == 0
        ? string.Create(CultureInfo.InvariantCulture, $"Line {line:0} is not in the log, which is empty.")
        : string.Create(CultureInfo.InvariantCulture, $"Line {line:0} is not in the log, which has lines 1 to {lineCount}.");

    private static IEnumerable<(JsonObject Finding, string Path)> Findings(JsonObject response)
    {
        if (response["findings"] is not JsonArray findings)
        {
            yield break;
        }

        for (var index = 0; index < findings.Count; index++)
        {
            if (findings[index] is JsonObject finding)
            {
                yield return (finding, ResponsePath.Item("findings", index));
            }
        }
    }

    // Null when the text is not exactly one JSON object.
    private static JsonObject? ParseObject(string text)
    {
        try
        {
            return JsonNode.Parse(text, documentOptions: StrictJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ValidationResult Reject(string code, string message) =>
        ValidationResult.Reject([new Violation(code, ResponsePath.Root, message)]);

    // Called only for a response that passed every check, so each field exists with the type the contract requires.
    private static Analysis ToAnalysis(JsonObject response) =>
        new((string)response["summary"]!, [.. response["findings"]!.AsArray().Select(finding => ToFinding(finding!.AsObject()))]);

    private static Finding ToFinding(JsonObject finding) => new(
        Title: (string)finding["title"]!,
        Severity: ClosedSetValue<Severity>(finding["severity"]),
        Category: ClosedSetValue<FindingCategory>(finding["category"]),
        LineRefs: [.. finding["line_refs"]!.AsArray().Select(LineNumber)],
        Evidence: (string)finding["evidence"]!,
        Explanation: (string)finding["explanation"]!,
        SuggestedAction: (string)finding["suggested_action"]!);

    // The schema check matched the value against the closed set regardless of letter case.
    private static T ClosedSetValue<T>(JsonNode? value)
        where T : struct, Enum => Enum.Parse<T>((string)value!, ignoreCase: true);

    private static int LineNumber(JsonNode? value)
    {
        ContractSchema.TryGetInteger(value, out var number);
        return (int)number;
    }
}
