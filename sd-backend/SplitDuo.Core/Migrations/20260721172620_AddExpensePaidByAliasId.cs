using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddExpensePaidByAliasId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "paid_by_alias_id",
                table: "expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_expenses_paid_by_alias_id",
                table: "expenses",
                column: "paid_by_alias_id");

            migrationBuilder.AddForeignKey(
                name: "FK_expenses_aliases_paid_by_alias_id",
                table: "expenses",
                column: "paid_by_alias_id",
                principalTable: "aliases",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expenses_aliases_paid_by_alias_id",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_paid_by_alias_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "paid_by_alias_id",
                table: "expenses");
        }
    }
}
