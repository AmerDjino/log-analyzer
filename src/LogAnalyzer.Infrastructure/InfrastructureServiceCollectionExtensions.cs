using LogAnalyzer.Core.Processing;
using LogAnalyzer.Infrastructure.Accounts;
using LogAnalyzer.Infrastructure.Models;
using LogAnalyzer.Infrastructure.Persistence;
using LogAnalyzer.Infrastructure.Runs;
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
    /// the account store, the run store, the background worker that analyzes runs, and the database health check.
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

        // Registered before data protection: hosted services start in registration order, and data protection
        // loads its keys at startup from a table that the first migration creates.
        services.AddSingleton<DatabaseMigrationState>();
        services.AddHostedService<DatabaseMigrator>();

        // Keys that protect session cookies live in the database, so rebuilding the API container does not sign everyone out.
        services.AddDataProtection()
            .SetApplicationName("LogAnalyzer")
            .PersistKeysToDbContext<LogAnalyzerDbContext>();

        // Accounts: the email address is the user name, and 5 failed sign-ins in a row lock the account for 15 minutes.
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Lockout.MaxFailedAccessAttempts = AccountRules.MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = AccountRules.LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<LogAnalyzerDbContext>()
            .AddSignInManager();

        // The registration endpoint checks the only password rule, its length. Identity's default validator would also
        // refuse a password made only of white space, which that rule allows.
        services.RemoveAll<IPasswordValidator<AppUser>>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<UserSessionRepository>();
        services.AddScoped<RunStore>();

        // Model:Provider chooses the model that analyzes runs: the stand-in unless configured.
        services.AddOptions<ModelOptions>()
            .BindConfiguration(ModelOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ModelOptions>, ModelOptionsValidation>();
        services.AddSingleton<IPromptTemplates, EmbeddedPromptTemplates>();
        services.AddSingleton(AnalysisModels.Create);
        services.AddScoped<IRunRecorder, RunRecorder>();
        services.AddScoped<AnalysisPipeline>();
        services.AddSingleton<RunProcessor>();
        services.AddSingleton<RunQueueSignal>();
        services.AddOptions<AnalysisOptions>()
            .BindConfiguration(AnalysisOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        // Registered after DatabaseMigrator, whose migrations it waits for.
        services.AddHostedService<RunWorker>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        return services;
    }
}
