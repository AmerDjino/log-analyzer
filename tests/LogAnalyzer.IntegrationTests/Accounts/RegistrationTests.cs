using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class RegistrationTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/new-account-is-created")]
    public async Task Register_CreatesAccountWithoutSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();

        using var response = await accounts.RegisterAsync(email, AccountsClient.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var account = await response.Content.ReadFromJsonAsync<AccountResponse>(cancellationToken);
        Assert.Equal(await database.ReadIdAsync(email, cancellationToken), account!.Id);
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

    // The scenarios pin 11 and 129 characters; these pin the rest of "Enforce password length". A character is a
    // Unicode code point, so an emoji counts once although a .NET string stores it as two UTF-16 units.
    [Fact]
    public async Task Register_AcceptsPasswordOf128Characters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        using var response = await accounts.RegisterAsync(AccountsClient.NewEmail(), new string('a', 128), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsPasswordOf11Emoji()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();

        using var response = await accounts.RegisterAsync(email, Emoji(11), cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "PASSWORD_TOO_SHORT", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    [Fact]
    public async Task Register_AcceptsPasswordOf128Emoji()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());

        using var response = await accounts.RegisterAsync(AccountsClient.NewEmail(), Emoji(128), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // No rule beyond length applies, so a password of spaces only is as valid as any other 12 characters.
    [Fact]
    public async Task Register_AcceptsPasswordOfTwelveSpaces()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = AccountsClient.NewEmail();
        var password = new string(' ', 12);

        using var response = await accounts.RegisterAsync(email, password, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var signIn = await accounts.SignInAsync(email, password, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    [Trait("Scenario", "user-accounts/malformed-registration-body-is-rejected")]
    public async Task Register_RejectsMalformedBody()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        var accountsBefore = await database.CountAllAccountsAsync(cancellationToken);

        // The body ends before its closing brace.
        using var body = new StringContent(
            $"{{\"email\":\"{email}\",\"password\":\"{AccountsClient.Password}\"", Encoding.UTF8, "application/json");
        using var response = await accounts.Http.PostAsync("/auth/register", body, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(accountsBefore, await database.CountAllAccountsAsync(cancellationToken));
    }

    // Bodies must be UTF-8, which RFC 8259 also requires for JSON. Reading a body in an unknown character set used to
    // throw, and the API answered 500.
    [Fact]
    public async Task Register_RejectsUnsupportedCharset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        using var body = new ByteArrayContent(
            JsonSerializer.SerializeToUtf8Bytes(new { email, password = AccountsClient.Password }));
        body.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=bogus");

        using var response = await accounts.Http.PostAsync("/auth/register", body, cancellationToken);

        await ProblemAssert.HasCodeAsync(
            response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_CHARSET", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    // SMTP allows no control characters in an address, and PostgreSQL cannot store a NUL. The address parser refuses a bare
    // NUL but accepts one escaped in a quoted local part, so that form is the one that has to be refused explicitly.
    [Fact]
    public async Task Register_RejectsEmailWithControlCharacter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var accountsBefore = await database.CountAllAccountsAsync(cancellationToken);

        using var response = await accounts.RegisterAsync(
            $"\"user-{Guid.NewGuid():N}\\\u0000x\"@example.com", AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "INVALID_EMAIL", cancellationToken);
        Assert.Equal(accountsBefore, await database.CountAllAccountsAsync(cancellationToken));
    }

    // The 254-character limit in "Register an account" has no scenario of its own; these pin both sides of it.
    [Fact]
    public async Task Register_AcceptsEmailOf254Characters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        var email = EmailOfLength(254);

        using var response = await accounts.RegisterAsync(email, AccountsClient.Password, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsEmailOf255Characters()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = EmailOfLength(255);

        using var response = await accounts.RegisterAsync(email, AccountsClient.Password, cancellationToken);

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "INVALID_EMAIL", cancellationToken);
        Assert.Equal(0, await database.CountAccountsAsync(email, cancellationToken));
    }

    /// <summary>
    /// A unique, otherwise valid address of exactly the given length (253 to 256): a 64-character local part,
    /// then domain labels of at most 63 characters, so only the length can make it invalid.
    /// </summary>
    private static string EmailOfLength(int length)
    {
        var localPart = $"user-{Guid.NewGuid():N}".PadRight(64, 'a');
        var email = $"{localPart}@{new string('b', 63)}.{new string('c', 63)}.{new string('d', length - 197)}.com";
        Assert.Equal(length, email.Length);
        return email;
    }

    /// <summary>The given number of U+1F600 (grinning face) characters, each stored as two UTF-16 units.</summary>
    private static string Emoji(int count) => string.Concat(Enumerable.Repeat("\U0001F600", count));
}
