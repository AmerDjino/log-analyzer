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
            case StopReason.Completed:
                break;
            case StopReason.OutputLimit:
                return Reject(
                    ReasonCodes.Truncated, "The model reached its output limit or its context window, so the response is cut off.");
            case StopReason.Refusal:
                return Reject(ReasonCodes.Refused, "The model declined to answer.");
            default:
                throw new ArgumentOutOfRangeException(nameof(input), input.StopReason, "Unknown stop reason.");
        }

        if (ParseObject(input.ResponseText, out var problem) is not { } response)
        {
            return Reject(
                ReasonCodes.NotJson,
                $"The response must be exactly one JSON object, with no code fence or other text around it. {problem}");
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
                    if (!JsonNumbers.TryGetWholeNumber(lineRefs[index], out var number))
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
                            ReasonCodes.LineOutOfRange,
                            ResponsePath.Item(lineRefsPath, index),
                            OutOfRangeMessage(JsonNumbers.Text(lineRefs[index]), logLines.Count)));
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

    // The line as the model wrote it, so 1e30 reads as 1e30 rather than as a rounded or clamped value.
    private static string OutOfRangeMessage(string line, int lineCount) => lineCount == 0
        ? $"Line {line} is not in the log, which is empty."
        : string.Create(CultureInfo.InvariantCulture, $"Line {line} is not in the log, which has lines 1 to {lineCount}.");

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

    // Null when the text is not exactly one JSON object; problem then says why.
    private static JsonObject? ParseObject(string text, out string problem)
    {
        try
        {
            var node = JsonNode.Parse(text, documentOptions: StrictJson);
            if (node is not JsonObject response)
            {
                problem = $"The text is valid JSON, but {Describe(node)}.";
                return null;
            }

            // Strings are decoded only when read, so read every one now: an unpaired surrogate such as \uD800 is then
            // reported here as NOT_JSON instead of escaping from a later check as an exception.
            DecodeStrings(response);
            problem = string.Empty;
            return response;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            problem = $"The parser reported: {exception.Message}";
            return null;
        }
    }

    private static void DecodeStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (_, child) in jsonObject)
                {
                    DecodeStrings(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    DecodeStrings(child);
                }

                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                _ = value.GetValue<string>();
                break;
        }
    }

    private static string Describe(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "an array",
        _ => node.GetValueKind() == JsonValueKind.String ? "a string" : "a single value",
    };

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

    // The citation check accepted only line numbers within the log.
    private static int LineNumber(JsonNode? value)
    {
        JsonNumbers.TryGetWholeNumber(value, out var number);
        return (int)number;
    }
}
