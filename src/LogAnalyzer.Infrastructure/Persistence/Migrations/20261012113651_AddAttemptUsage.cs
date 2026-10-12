using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogAnalyzer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttemptUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "content_sha256",
                table: "runs",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "runs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "input_tokens",
                table: "run_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "output_tokens",
                table: "run_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "request_id",
                table: "run_attempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "transport_retries",
                table: "run_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_runs_user_id_content_sha256",
                table: "runs",
                columns: new[] { "user_id", "content_sha256" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_runs_user_id_content_sha256",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "content_sha256",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "model",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "input_tokens",
                table: "run_attempts");

            migrationBuilder.DropColumn(
                name: "output_tokens",
                table: "run_attempts");

            migrationBuilder.DropColumn(
                name: "request_id",
                table: "run_attempts");

            migrationBuilder.DropColumn(
                name: "transport_retries",
                table: "run_attempts");
        }
    }
}
