using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>
/// Reports the API as unhealthy when PostgreSQL cannot answer a trivial query,
/// or when the schema migrations have not been applied yet.
/// </summary>
internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource, DatabaseMigrationState migrationState) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", exception);
        }

        return migrationState.IsApplied
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The database schema is not up to date yet.");
    }
}
