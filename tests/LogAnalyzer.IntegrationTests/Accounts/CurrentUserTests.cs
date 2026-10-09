using System.Net;
using System.Net.Http.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class CurrentUserTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/signed-in-user-is-returned")]
    public async Task Me_ReturnsSignedInUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        var signedIn = await accounts.RegisterAndSignInAsync(email, cancellationToken);

        using var response = await accounts.MeAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken);
        Assert.Equal(signedIn.Id, account!.Id);
        Assert.Equal(email, account.Email);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/request-without-a-session-is-refused")]
    public async Task Me_RefusesRequestWithoutSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClientWithoutCookies());

        using var response = await accounts.MeAsync(cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/tampered-session-cookie-is-refused")]
    public async Task Me_RefusesTamperedCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClientWithoutCookies());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        using var signIn = await accounts.SignInAsync(email, AccountsClient.Password, cancellationToken);
        var cookie = SessionCookie.RequireValue(signIn);

        using var request = SessionCookie.Request(HttpMethod.Get, "/auth/me", SessionCookie.Tamper(cookie));
        using var response = await accounts.Http.SendAsync(request, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "SIGN_IN_REQUIRED", cancellationToken);
    }
}
