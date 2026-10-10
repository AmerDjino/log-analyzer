using System.Globalization;

namespace LogAnalyzer.Api.Runs;

/// <summary>The error codes of the run endpoints. docs/runs-api.md lists the same codes.</summary>
internal static class RunErrors
{
    public static readonly ApiError UnsupportedContent = new(
        "UNSUPPORTED_CONTENT",
        StatusCodes.Status400BadRequest,
        "Unsupported content",
        "Submit the log as UTF-8 text without NUL bytes.");

    public static readonly ApiError EmptyInput = new(
        "EMPTY_INPUT",
        StatusCodes.Status400BadRequest,
        "Empty log",
        "The log contains nothing but white space.");

    // The detail names the configured limit, in MB of 1,048,576 bytes.
    public static ApiError InputTooLarge(int maxSubmissionBytes) => new(
        "INPUT_TOO_LARGE",
        StatusCodes.Status413PayloadTooLarge,
        "Log too large",
        string.Create(
            CultureInfo.InvariantCulture,
            $"Submit a log of at most {maxSubmissionBytes / 1_048_576.0:0.##} MB ({maxSubmissionBytes:N0} bytes)."));
}
