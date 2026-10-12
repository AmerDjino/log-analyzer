using LogAnalyzer.Infrastructure.Accounts;

namespace LogAnalyzer.Api.Accounts;

/// <summary>The error codes of the account endpoints. docs/accounts-api.md lists the same codes.</summary>
internal static class AccountErrors
{
    public static readonly ApiError InvalidEmail = new(
        ApiErrorCodes.InvalidEmail,
        StatusCodes.Status400BadRequest,
        "Invalid email address",
        $"Enter a valid email address of at most {AccountRules.MaxEmailLength} characters.");

    public static readonly ApiError EmailTaken = new(
        ApiErrorCodes.EmailTaken,
        StatusCodes.Status409Conflict,
        "Email address already registered",
        "An account with this email address already exists.");

    public static readonly ApiError PasswordTooShort = new(
        ApiErrorCodes.PasswordTooShort,
        StatusCodes.Status400BadRequest,
        "Password too short",
        $"Use a password of at least {AccountRules.MinPasswordLength} characters.");

    public static readonly ApiError PasswordTooLong = new(
        ApiErrorCodes.PasswordTooLong,
        StatusCodes.Status400BadRequest,
        "Password too long",
        $"Use a password of at most {AccountRules.MaxPasswordLength} characters.");

    // One entry for a wrong password and an unknown email address, so their answers are identical.
    public static readonly ApiError InvalidCredentials = new(
        ApiErrorCodes.InvalidCredentials,
        StatusCodes.Status401Unauthorized,
        "Sign-in failed",
        "The email address or password is incorrect.");

    public static readonly ApiError AccountLocked = new(
        ApiErrorCodes.AccountLocked,
        StatusCodes.Status403Forbidden,
        "Account locked",
        $"Too many failed sign-ins. Try again in {AccountRules.LockoutDuration.TotalMinutes:0} minutes.");

    public static readonly ApiError SignInRequired = new(
        ApiErrorCodes.SignInRequired,
        StatusCodes.Status401Unauthorized,
        "Sign-in required",
        "Sign in to use this endpoint.");
}
