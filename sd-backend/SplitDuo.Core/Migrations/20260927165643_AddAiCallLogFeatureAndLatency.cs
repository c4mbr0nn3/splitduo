using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCallLogFeatureAndLatency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "feature",
                table: "ai_call_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "latency_ms",
                table: "ai_call_logs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "feature",
                table: "ai_call_logs");

            migrationBuilder.DropColumn(
                name: "latency_ms",
                table: "ai_call_logs");
        }
    }
}
