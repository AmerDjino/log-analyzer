using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LogAnalyzer.IntegrationTests.Persistence;

public sealed class DataProtectionKeyTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Protect_StoresKeyInDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        // Protecting a value forces the key ring to create its first key.
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests");
        protector.Protect("value");

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM data_protection_keys", connection);
        var keyCount = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

        Assert.True(keyCount > 0);
    }
}
