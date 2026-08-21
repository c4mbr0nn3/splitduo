using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    filename_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_filename = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_attachments_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_attachments_expense_id_created_at",
                table: "expense_attachments",
                columns: new[] { "expense_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_expense_attachments_expense_id_file_hash",
                table: "expense_attachments",
                columns: new[] { "expense_id", "file_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_attachments_guid",
                table: "expense_attachments",
                column: "guid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_attachments");
        }
    }
}
