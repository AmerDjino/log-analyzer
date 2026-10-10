namespace LogAnalyzer.Core.Validation;

/// <summary>Why a response can be rejected. docs/output-contract.md explains each code.</summary>
public static class ReasonCodes
{
    /// <summary>The model stopped at its output limit.</summary>
    public const string Truncated = "TRUNCATED";

    /// <summary>The model declined to answer.</summary>
    public const string Refused = "REFUSED";

    /// <summary>The response is not exactly one JSON object.</summary>
    public const string NotJson = "NOT_JSON";

    /// <summary>The response breaks the output contract: a missing or unknown field, a wrong type or a value outside a closed set.</summary>
    public const string SchemaViolation = "SCHEMA_VIOLATION";

    /// <summary>A cited line number is not a line of the submitted log.</summary>
    public const string LineOutOfRange = "LINE_OUT_OF_RANGE";

    /// <summary>A finding's evidence does not appear on any of its cited lines.</summary>
    public const string EvidenceNotFound = "EVIDENCE_NOT_FOUND";

    /// <summary>A text field is empty or only whitespace.</summary>
    public const string BlankField = "BLANK_FIELD";

    /// <summary>A title or the summary is longer than its limit.</summary>
    public const string FieldTooLong = "FIELD_TOO_LONG";
}
