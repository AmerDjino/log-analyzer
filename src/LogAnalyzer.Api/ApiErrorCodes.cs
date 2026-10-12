using System.Reflection;

namespace LogAnalyzer.Api;

/// <summary>
/// Every code the API reports. The error tables take their codes from here, and the OpenAPI document lists
/// <see cref="All"/> as the values of the problem details' <c>code</c>.
/// </summary>
internal static class ApiErrorCodes
{
    public const string InvalidEmail = "INVALID_EMAIL";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordTooLong = "PASSWORD_TOO_LONG";
    public const string EmailTaken = "EMAIL_TAKEN";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string SignInRequired = "SIGN_IN_REQUIRED";
    public const string UnsupportedCharset = "UNSUPPORTED_CHARSET";
    public const string InputTooLarge = "INPUT_TOO_LARGE";
    public const string UnsupportedContent = "UNSUPPORTED_CONTENT";
    public const string EmptyInput = "EMPTY_INPUT";
    public const string RunNotFound = "RUN_NOT_FOUND";
    public const string InvalidLimit = "INVALID_LIMIT";
    public const string InvalidCursor = "INVALID_CURSOR";

    // Read from the constants above, in the order they are declared, so a new code needs no second edit here.
    public static readonly IReadOnlyList<string> All = typeof(ApiErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();
}
