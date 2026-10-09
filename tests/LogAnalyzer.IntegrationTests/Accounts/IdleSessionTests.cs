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
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);

        factory.Clock.Advance(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));
        using var response = await accounts.MeAsync(cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
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
}
