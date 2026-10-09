using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>Reports the API as unhealthy when PostgreSQL cannot answer a trivial query.</summary>
internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", exception);
        }
    }
}
