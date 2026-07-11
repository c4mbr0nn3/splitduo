using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamilyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "family_id",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_family_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                table: "refresh_tokens");
        }
    }
}
