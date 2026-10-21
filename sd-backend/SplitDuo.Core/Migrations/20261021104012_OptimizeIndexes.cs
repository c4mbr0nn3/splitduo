using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_deleted_at",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_guid",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_user_avatars_guid",
                table: "user_avatars");

            migrationBuilder.DropIndex(
                name: "IX_two_factor_tokens_expires_at",
                table: "two_factor_tokens");

            migrationBuilder.DropIndex(
                name: "IX_two_factor_tokens_token_type",
                table: "two_factor_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_family_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_jwt_id",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_templates_deleted_at",
                table: "recurring_expense_templates");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_templates_guid",
                table: "recurring_expense_templates");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_instances_deleted_at",
                table: "recurring_expense_instances");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_instances_guid",
                table: "recurring_expense_instances");

            migrationBuilder.DropIndex(
                name: "IX_notifications_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_created_at_sent_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_sent_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_sent_at_retry_count",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_invitation_tokens_email",
                table: "invitation_tokens");

            migrationBuilder.DropIndex(
                name: "IX_imports_group_id_import_date",
                table: "imports");

            migrationBuilder.DropIndex(
                name: "IX_imports_guid",
                table: "imports");

            migrationBuilder.DropIndex(
                name: "IX_groups_deleted_at",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_groups_guid",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_group_members_deleted_at",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_expenses_deleted_at",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_category_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_payment_mode_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_guid",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expense_attachments_guid",
                table: "expense_attachments");

            migrationBuilder.DropIndex(
                name: "IX_aliases_deleted_at",
                table: "aliases");

            migrationBuilder.DropIndex(
                name: "IX_aliases_guid",
                table: "aliases");

            migrationBuilder.DropIndex(
                name: "IX_ai_call_logs_requested_at",
                table: "ai_call_logs");

            migrationBuilder.CreateIndex(
                name: "IX_users_guid",
                table: "users",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_anchor_date",
                table: "recurring_expense_templates",
                column: "anchor_date",
                filter: "is_active AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_guid",
                table: "recurring_expense_templates",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_guid",
                table: "recurring_expense_instances",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_status_id",
                table: "recurring_expense_instances",
                column: "status_id",
                filter: "expense_id IS NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_retry_count",
                table: "notifications",
                column: "retry_count",
                filter: "sent_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_sent_at",
                table: "notifications",
                column: "sent_at",
                filter: "sent_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_guid",
                table: "invitation_tokens",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_imports_group_id_created_at",
                table: "imports",
                columns: new[] { "group_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_guid",
                table: "imports",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_guid",
                table: "groups",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_members_group_id_user_id",
                table: "group_members",
                columns: new[] { "group_id", "user_id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_category_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "category_id", "expense_date" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_expense_date_created_at",
                table: "expenses",
                columns: new[] { "group_id", "expense_date", "created_at" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "expense_type_id", "expense_date" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_guid",
                table: "expenses",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_attachments_guid",
                table: "expense_attachments",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aliases_guid",
                table: "aliases",
                column: "guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_call_logs_success_requested_at_id",
                table: "ai_call_logs",
                columns: new[] { "success", "requested_at", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_guid",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_templates_anchor_date",
                table: "recurring_expense_templates");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_templates_guid",
                table: "recurring_expense_templates");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_instances_guid",
                table: "recurring_expense_instances");

            migrationBuilder.DropIndex(
                name: "IX_recurring_expense_instances_status_id",
                table: "recurring_expense_instances");

            migrationBuilder.DropIndex(
                name: "IX_notifications_retry_count",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_sent_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_invitation_tokens_guid",
                table: "invitation_tokens");

            migrationBuilder.DropIndex(
                name: "IX_imports_group_id_created_at",
                table: "imports");

            migrationBuilder.DropIndex(
                name: "IX_imports_guid",
                table: "imports");

            migrationBuilder.DropIndex(
                name: "IX_groups_guid",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_group_members_group_id_user_id",
                table: "group_members");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_category_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_expense_date_created_at",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expenses_guid",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "IX_expense_attachments_guid",
                table: "expense_attachments");

            migrationBuilder.DropIndex(
                name: "IX_aliases_guid",
                table: "aliases");

            migrationBuilder.DropIndex(
                name: "IX_ai_call_logs_success_requested_at_id",
                table: "ai_call_logs");

            migrationBuilder.CreateIndex(
                name: "IX_users_deleted_at",
                table: "users",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_users_guid",
                table: "users",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_user_avatars_guid",
                table: "user_avatars",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_expires_at",
                table: "two_factor_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_token_type",
                table: "two_factor_tokens",
                column: "token_type");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_jwt_id",
                table: "refresh_tokens",
                column: "jwt_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_deleted_at",
                table: "recurring_expense_templates",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_templates_guid",
                table: "recurring_expense_templates",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_deleted_at",
                table: "recurring_expense_instances",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_expense_instances_guid",
                table: "recurring_expense_instances",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at",
                table: "notifications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_created_at_sent_at",
                table: "notifications",
                columns: new[] { "created_at", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_sent_at",
                table: "notifications",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_sent_at_retry_count",
                table: "notifications",
                columns: new[] { "sent_at", "retry_count" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_email",
                table: "invitation_tokens",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_imports_group_id_import_date",
                table: "imports",
                columns: new[] { "group_id", "import_date" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_guid",
                table: "imports",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_groups_deleted_at",
                table: "groups",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_groups_guid",
                table: "groups",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_deleted_at",
                table: "group_members",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_deleted_at",
                table: "expenses",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_expense_date",
                table: "expenses",
                column: "expense_date");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_category_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "category_id", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_expense_type_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "expense_type_id", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_group_id_payment_mode_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "payment_mode_id", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_guid",
                table: "expenses",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_expense_attachments_guid",
                table: "expense_attachments",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_aliases_deleted_at",
                table: "aliases",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_aliases_guid",
                table: "aliases",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_ai_call_logs_requested_at",
                table: "ai_call_logs",
                column: "requested_at");
        }
    }
}
