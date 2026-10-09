using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LogAnalyzer.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL data source and its health check.
    /// The connection string is read from <c>ConnectionStrings:Postgres</c> and checked when the host starts,
    /// so a missing setting stops the API immediately with a message naming it.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString =
                    configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"Connection string '{DatabaseOptions.ConnectionStringName}' is missing. " +
                $"Set ConnectionStrings__{DatabaseOptions.ConnectionStringName} or add it to user secrets (see README).")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new NpgsqlDataSourceBuilder(options.ConnectionString)
                .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                .Build();
        });

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
