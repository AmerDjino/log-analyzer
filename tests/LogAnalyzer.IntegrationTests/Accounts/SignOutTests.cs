using System.Net;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class SignOutTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/sign-out-clears-the-session-cookie")]
    public async Task SignOut_ExpiresSessionCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await accounts.RegisterAndSignInAsync(AccountsClient.NewEmail(), cancellationToken);

        using var response = await accounts.SignOutAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = SessionCookie.FindSetCookie(response);
        Assert.NotNull(setCookie);
        Assert.Equal(string.Empty, SessionCookie.ValueOf(setCookie));
        Assert.Contains(SessionCookie.AttributesOf(setCookie), attribute => attribute.StartsWith("expires=thu, 01 jan 1970", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/copied-cookie-stops-working-after-sign-out")]
    public async Task SignOut_InvalidatesCopiedCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClientWithoutCookies());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        using var signIn = await accounts.SignInAsync(email, AccountsClient.Password, cancellationToken);
        var copiedCookie = SessionCookie.RequireValue(signIn);

        using (var signOut = SessionCookie.Request(HttpMethod.Post, "/auth/sign-out", copiedCookie))
        using (var signedOut = await accounts.Http.SendAsync(signOut, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);
        }

        using var request = SessionCookie.Request(HttpMethod.Get, "/auth/me", copiedCookie);
        using var response = await accounts.Http.SendAsync(request, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
    }
}
