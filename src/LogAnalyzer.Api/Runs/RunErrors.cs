using System.Globalization;

namespace LogAnalyzer.Api.Runs;

/// <summary>The error codes of the run endpoints. docs/runs-api.md lists the same codes.</summary>
internal static class RunErrors
{
    public static readonly ApiError UnsupportedContent = new(
        ApiErrorCodes.UnsupportedContent,
        StatusCodes.Status400BadRequest,
        "Unsupported content",
        "Submit the log as UTF-8 text without NUL bytes.");

    public static readonly ApiError EmptyInput = new(
        ApiErrorCodes.EmptyInput,
        StatusCodes.Status400BadRequest,
        "Empty log",
        "The log contains nothing but white space.");

    // One answer for an unknown id, a malformed one and another person's run, so it reveals nothing about other runs.
    public static readonly ApiError RunNotFound = new(
        ApiErrorCodes.RunNotFound,
        StatusCodes.Status404NotFound,
        "Run not found",
        "You have no run with this id.");

    public static readonly ApiError InvalidLimit = new(
        ApiErrorCodes.InvalidLimit,
        StatusCodes.Status400BadRequest,
        "Invalid page size",
        $"Set limit to a whole number from 1 to {RunEndpoints.MaxPageSize}.");

    public static readonly ApiError InvalidCursor = new(
        ApiErrorCodes.InvalidCursor,
        StatusCodes.Status400BadRequest,
        "Invalid page cursor",
        "Use the nextCursor of a previous page, unchanged.");

    // The detail names the configured limit, in MB of 1,048,576 bytes.
    public static ApiError InputTooLarge(int maxSubmissionBytes) => new(
        ApiErrorCodes.InputTooLarge,
        StatusCodes.Status413PayloadTooLarge,
        "Log too large",
        string.Create(
            CultureInfo.InvariantCulture,
            $"Submit a log of at most {maxSubmissionBytes / 1_048_576.0:0.##} MB ({maxSubmissionBytes:N0} bytes)."));
}
