using LogAnalyzer.Infrastructure.Accounts;
using LogAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace LogAnalyzer.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PostgreSQL data source, the EF Core context, startup migrations, data-protection key storage,
    /// the account store and the database health check.
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

        services.AddDbContext<LogAnalyzerDbContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention());

        // Keys that protect session cookies live in the database, so rebuilding the API container does not sign everyone out.
        services.AddDataProtection()
            .SetApplicationName("LogAnalyzer")
            .PersistKeysToDbContext<LogAnalyzerDbContext>();

        // Accounts: the email address is the user name, passwords have a length rule only (no composition rules),
        // and 5 failed sign-ins in a row lock the account for 15 minutes.
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = AccountRules.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Lockout.MaxFailedAccessAttempts = AccountRules.MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = AccountRules.LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<LogAnalyzerDbContext>()
            .AddSignInManager();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserSessionRepository>();

        services.AddSingleton<DatabaseMigrationState>();
        services.AddHostedService<DatabaseMigrator>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
