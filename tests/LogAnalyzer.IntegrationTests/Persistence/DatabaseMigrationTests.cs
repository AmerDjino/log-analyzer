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
        Assert.Contains(migrations, id => id.EndsWith("_AddAttemptUsage", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Startup_AddsUsageAndReuseColumns()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await ReadStringsAsync(
            connection,
            """
            SELECT table_name || '.' || column_name || ' ' || data_type || ' ' || is_nullable || ' ' || coalesce(column_default, '')
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name IN ('runs', 'run_attempts')
            """,
            cancellationToken);
        Assert.Contains("run_attempts.input_tokens integer NO 0", columns);
        Assert.Contains("run_attempts.output_tokens integer NO 0", columns);
        Assert.Contains("run_attempts.transport_retries integer NO 0", columns);
        Assert.Contains("run_attempts.request_id text YES ", columns);
        Assert.Contains("runs.content_sha256 bytea YES ", columns);
        Assert.Contains("runs.prompt_version text YES ", columns);
        Assert.Contains("runs.model text YES ", columns);

        var indexes = await ReadStringsAsync(
            connection, "SELECT indexdef FROM pg_indexes WHERE tablename = 'runs'", cancellationToken);
        Assert.Contains(indexes, definition => definition.EndsWith("(user_id, content_sha256)", StringComparison.Ordinal));
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
