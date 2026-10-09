using LogAnalyzer.Infrastructure.Accounts;

namespace LogAnalyzer.Api.Accounts;

/// <summary>The error codes of the account endpoints. docs/accounts-api.md lists the same codes.</summary>
internal static class AccountErrors
{
    public static readonly ApiError InvalidEmail = new(
        "INVALID_EMAIL",
        StatusCodes.Status400BadRequest,
        "Invalid email address",
        $"Enter a valid email address of at most {AccountRules.MaxEmailLength} characters.");

    public static readonly ApiError EmailTaken = new(
        "EMAIL_TAKEN",
        StatusCodes.Status409Conflict,
        "Email address already registered",
        "An account with this email address already exists.");

    public static readonly ApiError PasswordTooShort = new(
        "PASSWORD_TOO_SHORT",
        StatusCodes.Status400BadRequest,
        "Password too short",
        $"Use a password of at least {AccountRules.MinPasswordLength} characters.");

    public static readonly ApiError PasswordTooLong = new(
        "PASSWORD_TOO_LONG",
        StatusCodes.Status400BadRequest,
        "Password too long",
        $"Use a password of at most {AccountRules.MaxPasswordLength} characters.");

    // One entry for a wrong password and an unknown email address, so their answers are identical.
    public static readonly ApiError InvalidCredentials = new(
        "INVALID_CREDENTIALS",
        StatusCodes.Status401Unauthorized,
        "Sign-in failed",
        "The email address or password is incorrect.");

    public static readonly ApiError AccountLocked = new(
        "ACCOUNT_LOCKED",
        StatusCodes.Status403Forbidden,
        "Account locked",
        $"Too many failed sign-ins. Try again in {AccountRules.LockoutDuration.TotalMinutes:0} minutes.");

    public static readonly ApiError SignInRequired = new(
        "SIGN_IN_REQUIRED",
        StatusCodes.Status401Unauthorized,
        "Sign-in required",
        "Sign in to use this endpoint.");
}
