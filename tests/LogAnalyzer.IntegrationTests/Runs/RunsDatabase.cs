using Npgsql;

namespace LogAnalyzer.IntegrationTests.Runs;

/// <summary>Reads and arranges stored runs with plain SQL, the way an operator would see them.</summary>
public sealed class RunsDatabase(string connectionString) : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();

    public async Task<long> CountRunsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var command = Command("SELECT count(*) FROM runs WHERE user_id = @id", userId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<StoredRun> ReadRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT user_id, file_name, status, byte_count, line_count FROM runs WHERE id = @id", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken), $"No stored run {runId}.");
        return new StoredRun(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4));
    }

    /// <summary>The run's stored lines, line N at index N - 1.</summary>
    public async Task<string[]> ReadLinesAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command("SELECT lines FROM run_logs WHERE run_id = @id", runId);
        return (string[])(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private NpgsqlCommand Command(string sql, Guid id)
    {
        var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return command;
    }
}

public sealed record StoredRun(Guid UserId, string? FileName, string Status, int ByteCount, int LineCount);
