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

    /// <summary>
    /// Completes a run the way processing does, in one transaction: the summary, the highest severity and the findings,
    /// in the order given. Values are in contract spelling, such as "critical".
    /// </summary>
    public async Task CompleteRunAsync(
        Guid runId,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        string summary,
        string? highestSeverity,
        IReadOnlyList<StoredFinding> findings,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = new NpgsqlCommand(
            """
            UPDATE runs SET status = 'completed', started_at = @startedAt, finished_at = @finishedAt,
                summary = @summary, highest_severity = @highestSeverity
            WHERE id = @id
            """,
            connection,
            transaction))
        {
            update.Parameters.AddWithValue("id", runId);
            update.Parameters.AddWithValue("startedAt", startedAt);
            update.Parameters.AddWithValue("finishedAt", finishedAt);
            update.Parameters.AddWithValue("summary", summary);
            update.Parameters.AddWithValue("highestSeverity", (object?)highestSeverity ?? DBNull.Value);
            Assert.Equal(1, await update.ExecuteNonQueryAsync(cancellationToken));
        }

        for (var position = 0; position < findings.Count; position++)
        {
            var finding = findings[position];
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO run_findings
                    (run_id, position, title, severity, category, line_refs, evidence, explanation, suggested_action)
                VALUES (@id, @position, @title, @severity, @category, @lineRefs, @evidence, @explanation, @suggestedAction)
                """,
                connection,
                transaction);
            insert.Parameters.AddWithValue("id", runId);
            insert.Parameters.AddWithValue("position", position);
            insert.Parameters.AddWithValue("title", finding.Title);
            insert.Parameters.AddWithValue("severity", finding.Severity);
            insert.Parameters.AddWithValue("category", finding.Category);
            insert.Parameters.AddWithValue("lineRefs", finding.LineRefs);
            insert.Parameters.AddWithValue("evidence", finding.Evidence);
            insert.Parameters.AddWithValue("explanation", finding.Explanation);
            insert.Parameters.AddWithValue("suggestedAction", finding.SuggestedAction);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private NpgsqlCommand Command(string sql, Guid id)
    {
        var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", id);
        return command;
    }
}

public sealed record StoredRun(Guid UserId, string? FileName, string Status, int ByteCount, int LineCount);

/// <summary>A finding as stored, in contract spelling.</summary>
public sealed record StoredFinding(
    string Title,
    string Severity,
    string Category,
    int[] LineRefs,
    string Evidence,
    string Explanation,
    string SuggestedAction);
