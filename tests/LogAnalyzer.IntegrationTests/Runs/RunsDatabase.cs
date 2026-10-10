using System.Text.Json;
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
            "SELECT user_id, file_name, status, failure_reason, byte_count, line_count FROM runs WHERE id = @id", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken), $"No stored run {runId}.");
        return new StoredRun(
            reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

    /// <summary>Sets a run to running, as if the API had stopped while it was processing it.</summary>
    public async Task SetRunningAsync(Guid runId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        await using var command = Command("UPDATE runs SET status = 'running', started_at = @startedAt WHERE id = @id", runId);
        command.Parameters.AddWithValue("startedAt", startedAt);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }

    /// <summary>The run's stored lines, line N at index N - 1.</summary>
    public async Task<string[]> ReadLinesAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command("SELECT lines FROM run_logs WHERE run_id = @id", runId);
        return (string[])(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// Queues a run for the person directly in the database, as another process would, so no signal wakes the worker.
    /// Its statistics are left at zero apart from the line count. Without <paramref name="withLines"/>, its lines are
    /// missing, so processing cannot read them.
    /// </summary>
    public async Task<Guid> InsertQueuedRunAsync(
        Guid userId, string[] lines, DateTimeOffset submittedAt, CancellationToken cancellationToken, bool withLines = true)
    {
        var runId = Guid.CreateVersion7(submittedAt);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var run = new NpgsqlCommand(
            """
            INSERT INTO runs (id, user_id, status, submitted_at, byte_count, line_count, critical_count, error_count,
                warning_count, info_count, debug_count, trace_count)
            VALUES (@id, @userId, 'queued', @submittedAt, 0, @lineCount, 0, 0, 0, 0, 0, 0)
            """,
            connection,
            transaction))
        {
            run.Parameters.AddWithValue("id", runId);
            run.Parameters.AddWithValue("userId", userId);
            run.Parameters.AddWithValue("submittedAt", submittedAt);
            run.Parameters.AddWithValue("lineCount", lines.Length);
            await run.ExecuteNonQueryAsync(cancellationToken);
        }

        if (withLines)
        {
            await using var log = new NpgsqlCommand(
                "INSERT INTO run_logs (run_id, lines) VALUES (@id, @lines)", connection, transaction);
            log.Parameters.AddWithValue("id", runId);
            log.Parameters.AddWithValue("lines", lines);
            await log.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return runId;
    }

    /// <summary>The run's attempts in order. No endpoint returns attempts yet.</summary>
    public async Task<List<StoredAttempt>> ReadAttemptsAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT sequence, outcome, rejection_reasons::text, model, prompt_version, started_at, finished_at
            FROM run_attempts WHERE run_id = @id ORDER BY sequence
            """,
            runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var attempts = new List<StoredAttempt>();
        while (await reader.ReadAsync(cancellationToken))
        {
            using var reasons = JsonDocument.Parse(reader.GetString(2));
            attempts.Add(new StoredAttempt(
                reader.GetInt32(0),
                reader.GetString(1),
                [.. reasons.RootElement.EnumerateArray().Select(reason =>
                    (reason.GetProperty("code").GetString()!, reason.GetProperty("path").GetString()!))],
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return attempts;
    }

    /// <summary>Every value stored for the run's attempts, as text, for checking what is never stored.</summary>
    public async Task<List<string>> ReadAttemptValuesAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command("SELECT * FROM run_attempts WHERE run_id = @id", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            for (var column = 0; column < reader.FieldCount; column++)
            {
                values.Add(reader.IsDBNull(column) ? string.Empty : Convert.ToString(reader.GetValue(column)) ?? string.Empty);
            }
        }

        return values;
    }

    public async Task<long> CountFindingsAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var command = Command("SELECT count(*) FROM run_findings WHERE run_id = @id", runId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
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

public sealed record StoredRun(
    Guid UserId, string? FileName, string Status, string? FailureReason, int ByteCount, int LineCount);

/// <summary>An attempt as stored: its outcome, and each rejection reason's code and path.</summary>
public sealed record StoredAttempt(
    int Sequence,
    string Outcome,
    List<(string Code, string Path)> RejectionReasons,
    string Model,
    string PromptVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);

/// <summary>A finding as stored, in contract spelling.</summary>
public sealed record StoredFinding(
    string Title,
    string Severity,
    string Category,
    int[] LineRefs,
    string Evidence,
    string Explanation,
    string SuggestedAction);
