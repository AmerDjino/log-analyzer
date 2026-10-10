using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogAnalyzer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    byte_count = table.Column<int>(type: "integer", nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    critical_count = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    warning_count = table.Column<int>(type: "integer", nullable: false),
                    info_count = table.Column<int>(type: "integer", nullable: false),
                    debug_count = table.Column<int>(type: "integer", nullable: false),
                    trace_count = table.Column<int>(type: "integer", nullable: false),
                    time_range_start = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_range_end = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_range_utc = table.Column<bool>(type: "boolean", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    highest_severity = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_runs", x => x.id);
                    table.CheckConstraint("ck_runs_failure_reason", "(status = 'failed') = (failure_reason IS NOT NULL)");
                    table.CheckConstraint("ck_runs_highest_severity", "highest_severity IN ('critical', 'error', 'warning', 'info')");
                    table.CheckConstraint("ck_runs_status", "status IN ('queued', 'running', 'completed', 'failed')");
                    table.CheckConstraint("ck_runs_summary", "(status = 'completed') = (summary IS NOT NULL)");
                    table.CheckConstraint("ck_runs_time_range", "(time_range_start IS NULL AND time_range_end IS NULL AND time_range_utc IS NULL) OR (time_range_start IS NOT NULL AND time_range_end IS NOT NULL AND time_range_utc IS NOT NULL AND time_range_start <= time_range_end)");
                    table.ForeignKey(
                        name: "fk_runs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_attempts",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    rejection_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    prompt_version = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_attempts", x => new { x.run_id, x.sequence });
                    table.CheckConstraint("ck_run_attempts_outcome", "outcome IN ('accepted', 'rejected')");
                    table.CheckConstraint("ck_run_attempts_rejection_reasons", "(outcome = 'rejected') = (jsonb_array_length(rejection_reasons) > 0)");
                    table.ForeignKey(
                        name: "fk_run_attempts_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_findings",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    line_refs = table.Column<int[]>(type: "integer[]", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    suggested_action = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_findings", x => new { x.run_id, x.position });
                    table.CheckConstraint("ck_run_findings_category", "category IN ('exception', 'timeout', 'dependency', 'resource', 'security', 'performance', 'configuration', 'other')");
                    table.CheckConstraint("ck_run_findings_severity", "severity IN ('critical', 'error', 'warning', 'info')");
                    table.ForeignKey(
                        name: "fk_run_findings_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_logs",
                columns: table => new
                {
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lines = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_logs", x => x.run_id);
                    table.ForeignKey(
                        name: "fk_run_logs_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_runs_submitted_at_id",
                table: "runs",
                columns: new[] { "submitted_at", "id" },
                filter: "status = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_runs_user_id_submitted_at_id",
                table: "runs",
                columns: new[] { "user_id", "submitted_at", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_attempts");

            migrationBuilder.DropTable(
                name: "run_findings");

            migrationBuilder.DropTable(
                name: "run_logs");

            migrationBuilder.DropTable(
                name: "runs");
        }
    }
}
