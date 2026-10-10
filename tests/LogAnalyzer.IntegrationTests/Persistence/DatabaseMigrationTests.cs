using Npgsql;

namespace LogAnalyzer.IntegrationTests.Persistence;

public sealed class DatabaseMigrationTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Startup_AppliesMigrations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Creating a client starts the API, which applies pending migrations during startup.
        using var client = factory.CreateClient();

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = await ReadStringsAsync(
            connection,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
            cancellationToken);
        Assert.Contains("__EFMigrationsHistory", tables);
        Assert.Contains("users", tables);
        Assert.Contains("user_sessions", tables);
        Assert.Contains("data_protection_keys", tables);
        Assert.Contains("runs", tables);
        Assert.Contains("run_logs", tables);
        Assert.Contains("run_findings", tables);
        Assert.Contains("run_attempts", tables);

        var migrations = await ReadStringsAsync(
            connection,
            """SELECT migration_id FROM "__EFMigrationsHistory" """,
            cancellationToken);
        Assert.Contains(migrations, id => id.EndsWith("_InitialAccounts", StringComparison.Ordinal));
        Assert.Contains(migrations, id => id.EndsWith("_AddRuns", StringComparison.Ordinal));
    }

    private static async Task<List<string>> ReadStringsAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
