using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "recurring_expense_template_id",
                table: "expenses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recurring_expense_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    payment_mode_id = table.Column<int>(type: "integer", nullable: false),
                    paid_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    paid_by_alias_id = table.Column<int>(type: "integer", nullable: true),
                    recurrence_mode_id = table.Column<int>(type: "integer", nullable: false),
                    weekdays = table.Column<int>(type: "integer", nullable: false),
                    day_of_month = table.Column<int>(type: "integer", nullable: true),
                    interval = table.Column<int>(type: "integer", nullable: true),
                    anchor_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    paused_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_templates_aliases_paid_by_alias_id",
                        column: x => x.paid_by_alias_id,
                        principalTable: "aliases",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recurring_expense_templates_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_templates_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_templates_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_instances",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    period_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    approved_by_id = table.Column<int>(type: "integer", nullable: true),
                    approved_at = table.Column<long>(type: "bigint", nullable: true),
                    expense_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_instances", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instances_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_recurring_expense_instances_recurring_expense_templates_tem~",
                        column: x => x.template_id,
                        principalTable: "recurring_expense_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instances_users_approved_by_id",
                        column: x => x.approved_by_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_template_alias_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    alias_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_template_alias_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_template_alias_splits_aliases_alias_id",
                        column: x => x.alias_id,
                        principalTable: "aliases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_template_alias_splits_recurring_expense_t~",
                        column: x => x.template_id,
                        principalTable: "recurring_expense_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_template_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_template_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_template_splits_recurring_expense_templat~",
                        column: x => x.template_id,
                        principalTable: "recurring_expense_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_template_splits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_instance_alias_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_id = table.Column<int>(type: "integer", nullable: false),
                    alias_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_instance_alias_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instance_alias_splits_aliases_alias_id",
                        column: x => x.alias_id,
                        principalTable: "aliases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instance_alias_splits_recurring_expense_i~",
                        column: x => x.instance_id,
                        principalTable: "recurring_expense_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_instance_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instance_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_expense_instance_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instance_splits_recurring_expense_instanc~",
                        column: x => x.instance_id,
                        principalTable: "recurring_expense_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recurring_expense_instance_splits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_recurring_expense_template_id",
                table: "expenses",
                column: "recurring_expense_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instance_alias_splits_alias_id",
                table: "recurring_expense_instance_alias_splits",
                column: "alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instance_alias_splits_instance_id",
                table: "recurring_expense_instance_alias_splits",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instance_splits_instance_id",
                table: "recurring_expense_instance_splits",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instance_splits_user_id",
                table: "recurring_expense_instance_splits",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_approved_by_id",
                table: "recurring_expense_instances",
                column: "approved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_deleted_at",
                table: "recurring_expense_instances",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_expense_id",
                table: "recurring_expense_instances",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_guid",
                table: "recurring_expense_instances",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_template_id_period_date",
                table: "recurring_expense_instances",
                columns: new[] { "template_id", "period_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_template_id_status_id",
                table: "recurring_expense_instances",
                columns: new[] { "template_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_template_alias_splits_alias_id",
                table: "recurring_expense_template_alias_splits",
                column: "alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_template_alias_splits_template_id",
                table: "recurring_expense_template_alias_splits",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_template_splits_template_id",
                table: "recurring_expense_template_splits",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_template_splits_user_id",
                table: "recurring_expense_template_splits",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_deleted_at",
                table: "recurring_expense_templates",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_group_id_is_active",
                table: "recurring_expense_templates",
                columns: new[] { "group_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_guid",
                table: "recurring_expense_templates",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_owner_id",
                table: "recurring_expense_templates",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_paid_by_alias_id",
                table: "recurring_expense_templates",
                column: "paid_by_alias_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_paid_by_user_id",
                table: "recurring_expense_templates",
                column: "paid_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_expenses_recurring_expense_templates_recurring_expense_temp~",
                table: "expenses",
                column: "recurring_expense_template_id",
                principalTable: "recurring_expense_templates",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expenses_recurring_expense_templates_recurring_expense_temp~",
                table: "expenses");

            migrationBuilder.DropTable(
                name: "recurring_expense_instance_alias_splits");

            migrationBuilder.DropTable(
                name: "recurring_expense_instance_splits");

            migrationBuilder.DropTable(
                name: "recurring_expense_template_alias_splits");

            migrationBuilder.DropTable(
                name: "recurring_expense_template_splits");

            migrationBuilder.DropTable(
                name: "recurring_expense_instances");

            migrationBuilder.DropTable(
                name: "recurring_expense_templates");

            migrationBuilder.DropIndex(
                name: "IX_expenses_recurring_expense_template_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "recurring_expense_template_id",
                table: "expenses");
        }
    }
}
