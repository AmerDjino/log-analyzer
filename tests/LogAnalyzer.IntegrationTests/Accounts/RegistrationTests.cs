using System.Net;
using System.Net.Http.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class RegistrationTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/new-account-is-created")]
    public async Task Register_CreatesAccountWithoutSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();

        using var response = await accounts.RegisterAsync(email, AccountsClient.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken);
        Assert.NotEqual(Guid.Empty, account!.Id);
        Assert.Equal(email, account.Email);
        Assert.DoesNotContain(AccountsClient.Password, body, StringComparison.Ordinal);
        ProblemAssert.SetsNoCookie(response);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/invalid-email-address-is-rejected")]
    public async Task Register_RejectsInvalidEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = $"user-{Guid.NewGuid():N}-at-example.com";

        using var response = await accounts.RegisterAsync(email, AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "INVALID_EMAIL", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/email-address-in-different-case-is-taken")]
    public async Task Register_RejectsEmailTakenInDifferentCase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        using var response = await accounts.RegisterAsync(email.ToUpperInvariant(), AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.Conflict, "EMAIL_TAKEN", cancellationToken);
        Assert.Equal(1, await database.CountAccountsAsync(email, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/short-password-is-rejected")]
    public async Task Register_RejectsShortPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();

        using var response = await accounts.RegisterAsync(email, new string('a', 11), cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "PASSWORD_TOO_SHORT", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/overlong-password-is-rejected")]
    public async Task Register_RejectsOverlongPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();

        using var response = await accounts.RegisterAsync(email, new string('a', 129), cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "PASSWORD_TOO_LONG", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/twelve-lowercase-letters-are-accepted")]
    public async Task Register_AcceptsTwelveLowercaseLetters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        using var response = await accounts.RegisterAsync(AccountsClient.NewEmail(), "abcdefghijkl", cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
