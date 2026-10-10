using System.Net;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class IdleSessionTests(TimedPostgresApiFactory factory) : IClassFixture<TimedPostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/idle-session-ends-after-8-hours")]
    public async Task Session_EndsAfterEightIdleHours()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        await accounts.RegisterAndSignInAsync(email, cancellationToken);
        Assert.Equal(1, await database.CountSessionsAsync(email, cancellationToken));

        factory.Clock.Advance(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));
        using var response = await accounts.MeAsync(cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
        // Presenting the ended session deletes its stored row.
        Assert.Equal(0, await database.CountSessionsAsync(email, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/using-a-session-keeps-it-alive")]
    public async Task Session_StaysAliveWhileUsed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        // Signed in at "09:00", used at "12:00", used again at "19:30": never 8 hours without use.
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);
        factory.Clock.Advance(TimeSpan.FromHours(3));
        using (var atNoon = await accounts.MeAsync(cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, atNoon.StatusCode);
        }

        factory.Clock.Advance(TimeSpan.FromHours(7.5));
        using var response = await accounts.MeAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/a-minute-of-use-renews-the-session")]
    public async Task Session_RenewsWhenUsedAfterOneMinute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        // Signed in at "09:00" and used at "09:01", so the session lasts until "17:01". Unrenewed, it would end at "17:00".
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        using (var afterOneMinute = await accounts.MeAsync(cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, afterOneMinute.StatusCode);
        }

        factory.Clock.Advance(TimeSpan.FromHours(8) - TimeSpan.FromSeconds(30));
        using var response = await accounts.MeAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
