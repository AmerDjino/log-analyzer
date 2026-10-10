using LogAnalyzer.Infrastructure.Accounts;
using LogAnalyzer.Infrastructure.Runs;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogAnalyzer.Infrastructure.Persistence;

/// <summary>
/// The application's database: accounts (Identity's user tables, no roles), sessions, the keys that protect session
/// cookies, and runs with their lines, findings and attempts. Names are snake_case.
/// </summary>
public sealed class LogAnalyzerDbContext(DbContextOptions<LogAnalyzerDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunLog> RunLogs => Set<RunLog>();

    public DbSet<RunFinding> RunFindings => Set<RunFinding>();

    public DbSet<RunAttempt> RunAttempts => Set<RunAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        builder.Entity<UserSession>(session =>
        {
            session.ToTable("user_sessions");
            session.HasKey(s => s.Id);
            session.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            session.HasIndex(s => s.UserId);
        });

        RunModel.Configure(builder);
    }
}
