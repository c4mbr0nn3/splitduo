using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_email",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "pending_email",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pending_email_expires_at",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_email_token_hash",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Normalize legacy rows before the case-insensitive unique index exists.
            migrationBuilder.Sql(
                "UPDATE users SET email = lower(email) WHERE email <> lower(email);");

            // Raw-SQL expression index: EF Core cannot model lower(email). Creating it
            // after the backfill makes a legacy collision fail loudly instead of silently
            // merging or renaming accounts; the operator must resolve it by hand.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX idx_users_email_lower ON users (lower(email)) WHERE deleted_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_users_email_lower;");

            migrationBuilder.DropColumn(
                name: "pending_email",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_email_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pending_email_token_hash",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }
    }
}
