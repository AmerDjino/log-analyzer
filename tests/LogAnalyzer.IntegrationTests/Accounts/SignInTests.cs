using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

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

    // No account can have such an address, and PostgreSQL cannot compare a NUL, so it must get the unknown-address answer.
    [Fact]
    public async Task SignIn_AnswersEmailWithControlCharacterLikeUnknownEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        using var response = await accounts.SignInAsync(
            $"user-{Guid.NewGuid():N}\u0000x@example.com", AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", cancellationToken);
        ProblemAssert.SetsNoCookie(response);
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

    [Fact]
    [Trait("Scenario", "user-accounts/empty-sign-in-body-is-rejected")]
    public async Task SignIn_RejectsEmptyBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        using var body = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await accounts.Http.PostAsync("/auth/sign-in", body, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ProblemAssert.SetsNoCookie(response);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/sign-in-body-in-an-unsupported-character-set-is-rejected")]
    public async Task SignIn_RejectsUnsupportedCharset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        // The right credentials, so only the declared character set can stop the sign-in.
        using var body = new ByteArrayContent(
            JsonSerializer.SerializeToUtf8Bytes(new { email, password = AccountsClient.Password }));
        body.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=bogus");

        using var response = await accounts.Http.PostAsync("/auth/sign-in", body, cancellationToken);

        await ProblemAssert.HasCodeAsync(
            response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_CHARSET", cancellationToken);
        ProblemAssert.SetsNoCookie(response);
    }

    // Every spelling of UTF-8 that the charset check lets through must also be readable as JSON.
    [Theory]
    [InlineData("application/json; charset=UTF-8")]
    [InlineData("application/json; charset=\"utf-8\"")]
    [InlineData("application/json; charset=utf8")]
    public async Task SignIn_AcceptsUtf8InEverySpelling(string contentType)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);
        using var body = new ByteArrayContent(
            JsonSerializer.SerializeToUtf8Bytes(new { email, password = AccountsClient.Password }));
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        using var response = await accounts.Http.PostAsync("/auth/sign-in", body, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
