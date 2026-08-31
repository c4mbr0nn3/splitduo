using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "expense_type_id",
                table: "expenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "expense_type_id", "expense_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "expense_type_id",
                table: "expenses");
        }
    }
}
