using Npgsql;

namespace LogAnalyzer.IntegrationTests.Accounts;

/// <summary>Reads and arranges stored accounts with plain SQL, the way an operator would see them.</summary>
public sealed class AccountsDatabase(string connectionString) : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    public async Task<long> CountAllAccountsAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("SELECT count(*) FROM users");
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<long> CountAccountsAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT count(*) FROM users WHERE normalized_email = upper(@email)", email);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<Guid> ReadIdAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT id FROM users WHERE normalized_email = upper(@email)", email);
        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<long> CountSessionsAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT count(*) FROM user_sessions s JOIN users u ON u.id = s.user_id WHERE u.normalized_email = upper(@email)",
            email);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>Every column of the account, as text.</summary>
    public async Task<List<string>> ReadStoredValuesAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT * FROM users WHERE normalized_email = upper(@email)", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken), $"No stored account for {email}.");
        var values = new List<string>();
        for (var column = 0; column < reader.FieldCount; column++)
        {
            values.Add(reader.IsDBNull(column) ? string.Empty : Convert.ToString(reader.GetValue(column)) ?? string.Empty);
        }

        return values;
    }

    public async Task<string> ReadPasswordHashAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT password_hash FROM users WHERE normalized_email = upper(@email)", email);
        return (string)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<DateTimeOffset?> ReadLockoutEndAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT lockout_end FROM users WHERE normalized_email = upper(@email)", email);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTime utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : value as DateTimeOffset?;
    }

    public async Task SetLockoutEndAsync(string email, DateTimeOffset lockoutEnd, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "UPDATE users SET lockout_end = @lockoutEnd WHERE normalized_email = upper(@email)", email);
        command.Parameters.AddWithValue("lockoutEnd", lockoutEnd);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    private NpgsqlCommand Command(string sql, string email)
    {
        var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("email", email);
        return command;
    }
}
