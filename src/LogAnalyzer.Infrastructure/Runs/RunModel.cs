using System.Text.Json;
using LogAnalyzer.Core.Contract;
using LogAnalyzer.Infrastructure.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LogAnalyzer.Infrastructure.Runs;

/// <summary>
/// Maps the run tables: runs, run_logs, run_findings and run_attempts. Statuses, outcomes, severities and categories
/// are lowercase text with CHECK constraints. failure_reason has no CHECK on its value, so later changes can add
/// reasons without a migration.
/// </summary>
internal static class RunModel
{
    private static readonly ValueConverter<Severity, string> SeverityConverter =
        new(severity => severity.ToContractValue(), text => Enum.Parse<Severity>(text, true));

    private static readonly ValueConverter<FindingCategory, string> CategoryConverter =
        new(category => category.ToContractValue(), text => Enum.Parse<FindingCategory>(text, true));

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Run>(run =>
        {
            run.ToTable("runs", table =>
            {
                table.HasCheckConstraint("ck_runs_status", $"status IN ({Values<RunStatus>(Lowercase)})");
                table.HasCheckConstraint("ck_runs_failure_reason", "(status = 'failed') = (failure_reason IS NOT NULL)");
                table.HasCheckConstraint("ck_runs_summary", "(status = 'completed') = (summary IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_runs_highest_severity", $"highest_severity IN ({Values<Severity>(ContractValues.ToContractValue)})");
                table.HasCheckConstraint(
                    "ck_runs_time_range",
                    "(time_range_start IS NULL AND time_range_end IS NULL AND time_range_utc IS NULL) OR "
                    + "(time_range_start IS NOT NULL AND time_range_end IS NOT NULL AND time_range_utc IS NOT NULL "
                    + "AND time_range_start <= time_range_end)");
            });
            run.HasKey(r => r.Id);
            run.Property(r => r.Id).ValueGeneratedNever();
            run.HasOne<AppUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            run.Property(r => r.Status).HasConversion(LowercaseConverter<RunStatus>());
            run.Property(r => r.HighestSeverity).HasConversion(SeverityConverter);
            // Times as the log wrote them have no offset, so neither column converts to or from a time zone.
            run.Property(r => r.TimeRangeStart).HasColumnType("timestamp without time zone");
            run.Property(r => r.TimeRangeEnd).HasColumnType("timestamp without time zone");
            run.HasMany(r => r.Findings).WithOne().HasForeignKey(f => f.RunId).OnDelete(DeleteBehavior.Cascade);
            run.HasMany(r => r.Attempts).WithOne().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);

            // Listing a person's runs, newest first; it also serves the foreign key to users.
            run.HasIndex(r => new { r.UserId, r.SubmittedAt, r.Id }).IsDescending(false, true, true);
            // Claiming the oldest queued run.
            run.HasIndex(r => new { r.SubmittedAt, r.Id }).HasFilter("status = 'queued'");
        });

        builder.Entity<RunLog>(log =>
        {
            log.ToTable("run_logs");
            log.HasKey(l => l.RunId);
            log.HasOne<Run>().WithOne().HasForeignKey<RunLog>(l => l.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RunFinding>(finding =>
        {
            finding.ToTable("run_findings", table =>
            {
                table.HasCheckConstraint(
                    "ck_run_findings_severity", $"severity IN ({Values<Severity>(ContractValues.ToContractValue)})");
                table.HasCheckConstraint(
                    "ck_run_findings_category", $"category IN ({Values<FindingCategory>(ContractValues.ToContractValue)})");
            });
            finding.HasKey(f => new { f.RunId, f.Position });
            finding.Property(f => f.Severity).HasConversion(SeverityConverter);
            finding.Property(f => f.Category).HasConversion(CategoryConverter);
        });

        builder.Entity<RunAttempt>(attempt =>
        {
            attempt.ToTable("run_attempts", table =>
            {
                table.HasCheckConstraint("ck_run_attempts_outcome", $"outcome IN ({Values<AttemptOutcome>(Lowercase)})");
                table.HasCheckConstraint(
                    "ck_run_attempts_rejection_reasons",
                    "(outcome = 'rejected') = (jsonb_array_length(rejection_reasons) > 0)");
            });
            attempt.HasKey(a => new { a.RunId, a.Sequence });
            attempt.Property(a => a.Outcome).HasConversion(LowercaseConverter<AttemptOutcome>());
            // [{"code": ..., "path": ...}]
            attempt.Property(a => a.RejectionReasons)
                .HasColumnType("jsonb")
                .HasConversion(
                    reasons => JsonSerializer.Serialize(reasons, JsonSerializerOptions.Web),
                    json => JsonSerializer.Deserialize<List<RejectionReason>>(json, JsonSerializerOptions.Web)!,
                    new ValueComparer<List<RejectionReason>>(
                        (left, right) => left!.SequenceEqual(right!),
                        reasons => reasons.Aggregate(0, (hash, reason) => HashCode.Combine(hash, reason)),
                        reasons => reasons.ToList()));
        });
    }

    private static ValueConverter<T, string> LowercaseConverter<T>()
        where T : struct, Enum =>
        new(value => Lowercase(value), text => Enum.Parse<T>(text, true));

    private static string Lowercase<T>(T value)
        where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    // The CHECK constraint's list of allowed values, such as 'queued', 'running'.
    private static string Values<T>(Func<T, string> spelling)
        where T : struct, Enum =>
        string.Join(", ", Enum.GetValues<T>().Select(value => $"'{spelling(value)}'"));
}
