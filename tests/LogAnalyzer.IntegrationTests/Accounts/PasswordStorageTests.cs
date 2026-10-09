namespace LogAnalyzer.IntegrationTests.Accounts;

public sealed class PasswordStorageTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    [Trait("Scenario", "user-accounts/stored-account-does-not-contain-the-password")]
    public async Task StoredAccount_DoesNotContainPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var email = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(email, cancellationToken);

        var storedValues = await database.ReadStoredValuesAsync(email, cancellationToken);

        Assert.DoesNotContain(storedValues, value => value.Contains(AccountsClient.Password, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Scenario", "user-accounts/same-password-gives-different-stored-hashes")]
    public async Task SamePassword_GivesDifferentHashes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var accounts = new AccountsClient(factory.CreateClient());
        await using var database = new AccountsDatabase(factory.ConnectionString);
        var first = AccountsClient.NewEmail();
        var second = AccountsClient.NewEmail();
        await accounts.RegisterExistingAsync(first, cancellationToken);
        await accounts.RegisterExistingAsync(second, cancellationToken);

        var firstHash = await database.ReadPasswordHashAsync(first, cancellationToken);
        var secondHash = await database.ReadPasswordHashAsync(second, cancellationToken);

        Assert.False(string.IsNullOrEmpty(firstHash));
        Assert.NotEqual(firstHash, secondHash);
    }
}
