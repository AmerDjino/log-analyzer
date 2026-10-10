using System.Net;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class LockoutTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string WrongPassword = "a wrong password";

    [Fact]
    [Trait("Scenario", "user-accounts/fifth-failed-sign-in-locks-the-account")]
    public async Task FifthFailedSignIn_LocksAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        await FailSignInAsync(accounts, email, times: 4, cancellationToken);

        var fifthFailureAt = DateTimeOffset.UtcNow;
        using var fifth = await accounts.SignInAsync(email, WrongPassword, cancellationToken);
        using var correct = await accounts.SignInAsync(email, AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(fifth, HttpStatusCode.Forbidden, "ACCOUNT_LOCKED", cancellationToken);
        await ProblemAssert.HasCodeAsync(correct, HttpStatusCode.Forbidden, "ACCOUNT_LOCKED", cancellationToken);
        ProblemAssert.SetsNoCookie(correct);

        var lockoutEnd = await database.ReadLockoutEndAsync(email, cancellationToken);
        Assert.NotNull(lockoutEnd);
        var lockDuration = lockoutEnd.Value - fifthFailureAt;
        Assert.InRange(lockDuration, TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(30));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/lock-ends-after-15-minutes")]
    public async Task Lock_EndsAfterFifteenMinutes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        // Locked 15 minutes and 1 second ago: the 15-minute lock ended a second ago.
        await database.SetLockoutEndAsync(email, DateTimeOffset.UtcNow.AddSeconds(-1), cancellationToken);
        using var response = await accounts.SignInAsync(email, AccountsClient.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(SessionCookie.FindSetCookie(response));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/successful-sign-in-resets-the-failure-count")]
    public async Task SuccessfulSignIn_ResetsFailureCount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClientWithoutCookies());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        await FailSignInAsync(accounts, email, times: 4, cancellationToken);
        using (var success = await accounts.SignInAsync(email, AccountsClient.Password, cancellationToken))
        {
            success.EnsureSuccessStatusCode();
        }

        using var response = await accounts.SignInAsync(email, WrongPassword, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
    }

    private static async Task FailSignInAsync(AccountsClient accounts, string email, int times, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            using var response = await accounts.SignInAsync(email, WrongPassword, cancellationToken);
            await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
        }
    }
}
