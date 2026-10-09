using System.Net;
using System.Net.Http.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class SignInTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/correct-credentials-start-a-session")]
    public async Task SignIn_StartsSessionWithSecureCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        using var response = await accounts.SignInAsync(email.ToUpperInvariant(), AccountsClient.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken);
        Assert.NotEqual(Guid.Empty, account!.Id);
        Assert.Equal(email, account.Email);
        var setCookie = SessionCookie.FindSetCookie(response);
        Assert.NotNull(setCookie);
        var attributes = SessionCookie.AttributesOf(setCookie);
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/wrong-password-is-rejected")]
    public async Task SignIn_RejectsWrongPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        using var response = await accounts.SignInAsync(email, "a wrong password", cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
        ProblemAssert.SetsNoCookie(response);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/unknown-email-gets-the-same-answer-as-a-wrong-password")]
    public async Task SignIn_AnswersUnknownEmailLikeWrongPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        using var wrongPassword = await accounts.SignInAsync(email, "a wrong password", cancellationToken);
        using var unknownEmail = await accounts.SignInAsync(AccountsClient.NewEmail(), "any password at all", cancellationToken);

        var expected = await ProblemAssert.HasCodeAsync(
            wrongPassword, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
        var actual = await ProblemAssert.HasCodeAsync(
            unknownEmail, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
        Assert.Equal(expected.GetProperty("title").GetString(), actual.GetProperty("title").GetString());
        Assert.Equal(expected.GetProperty("detail").GetString(), actual.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Scenario", "user-accounts/form-encoded-sign-in-is-rejected")]
    public async Task SignIn_RejectsFormEncodedBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = AccountsClient.Password,
        });

        using var response = await accounts.Http.PostAsync("/auth/sign-in", form, cancellationToken);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        ProblemAssert.SetsNoCookie(response);
    }
}
