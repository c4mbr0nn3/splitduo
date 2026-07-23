using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupMemberAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "alias_setup_finalized",
                table: "groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "use_aliases",
                table: "groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "alias_id",
                table: "group_members",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aliases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_singleton = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_aliases_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_alias_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    alias_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_alias_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_alias_splits_aliases_alias_id",
                        column: x => x.alias_id,
                        principalTable: "aliases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expense_alias_splits_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_members_alias_id",
                table: "group_members",
                column: "alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_aliases_deleted_at",
                table: "aliases",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_aliases_group_id",
                table: "aliases",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_aliases_guid",
                table: "aliases",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_expense_alias_splits_alias_id",
                table: "expense_alias_splits",
                column: "alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_alias_splits_expense_id",
                table: "expense_alias_splits",
                column: "expense_id");

            migrationBuilder.AddForeignKey(
                name: "FK_group_members_aliases_alias_id",
                table: "group_members",
                column: "alias_id",
                principalTable: "aliases",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_group_members_aliases_alias_id",
                table: "group_members");

            migrationBuilder.DropTable(
                name: "expense_alias_splits");

            migrationBuilder.DropTable(
                name: "aliases");

            migrationBuilder.DropIndex(
                name: "IX_group_members_alias_id",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "alias_setup_finalized",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "use_aliases",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "alias_id",
                table: "group_members");
        }
    }
}
