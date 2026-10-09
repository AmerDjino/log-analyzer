namespace LogAnalyzer.Infrastructure.Accounts;

/// <summary>The limits from the user-accounts spec, in one place for the store options and the endpoints.</summary>
public static class AccountRules
{
    public const int MinPasswordLength = 12;

    public const int MaxPasswordLength = 128;

    public const int MaxEmailLength = 254;

    public const int MaxFailedSignIns = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
}
