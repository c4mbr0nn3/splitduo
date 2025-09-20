using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    to = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    sent_at = table.Column<long>(type: "bigint", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    global_role_id = table.Column<int>(type: "integer", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    totp_secret = table.Column<string>(type: "text", nullable: true),
                    backup_codes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_by = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_groups_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    jwt_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<long>(type: "bigint", nullable: false),
                    revoked_at = table.Column<long>(type: "bigint", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    client_info = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "two_factor_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    token_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<long>(type: "bigint", nullable: false),
                    used_at = table.Column<long>(type: "bigint", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    client_info = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_two_factor_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_two_factor_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    paid_by = table.Column<int>(type: "integer", nullable: false),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    payment_mode_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_expenses_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expenses_users_paid_by",
                        column: x => x.paid_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "imports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    filename = table.Column<string>(type: "text", nullable: false),
                    file_hash = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    import_date = table.Column<DateOnly>(type: "date", nullable: false),
                    records_count = table.Column<int>(type: "integer", nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    import_type_id = table.Column<int>(type: "integer", nullable: false),
                    error_details = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<long>(type: "bigint", nullable: true),
                    completed_at = table.Column<long>(type: "bigint", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imports", x => x.id);
                    table.ForeignKey(
                        name: "FK_imports_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_imports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "settlements",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    from_user_id = table.Column<int>(type: "integer", nullable: false),
                    to_user_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false),
                    deleted_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlements", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlements_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_settlements_users_from_user_id",
                        column: x => x.from_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_settlements_users_to_user_id",
                        column: x => x.to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_splits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    split_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_splits", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_splits_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_expense_splits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_splits_expense_id",
                table: "expense_splits",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_splits_user_id",
                table: "expense_splits",
                column: "user_id");

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
                name: "IX_expenses_group_id_payment_mode_id_expense_date",
                table: "expenses",
                columns: new[] { "group_id", "payment_mode_id", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_guid",
                table: "expenses",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_paid_by_expense_date",
                table: "expenses",
                columns: new[] { "paid_by", "expense_date" });

            migrationBuilder.CreateIndex(
                name: "IX_group_members_deleted_at",
                table: "group_members",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_group_id",
                table: "group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_user_id",
                table: "group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_created_by",
                table: "groups",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_groups_deleted_at",
                table: "groups",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_groups_guid",
                table: "groups",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_imports_group_id_file_hash",
                table: "imports",
                columns: new[] { "group_id", "file_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_imports_group_id_import_date",
                table: "imports",
                columns: new[] { "group_id", "import_date" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_guid",
                table: "imports",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_imports_status_id_import_date",
                table: "imports",
                columns: new[] { "status_id", "import_date" });

            migrationBuilder.CreateIndex(
                name: "IX_imports_user_id_import_date",
                table: "imports",
                columns: new[] { "user_id", "import_date" });

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
                name: "IX_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_jwt_id",
                table: "refresh_tokens",
                column: "jwt_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_revoked_at",
                table: "refresh_tokens",
                columns: new[] { "user_id", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_deleted_at",
                table: "settlements",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_from_user_id_settlement_date",
                table: "settlements",
                columns: new[] { "from_user_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_group_id_settlement_date",
                table: "settlements",
                columns: new[] { "group_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "IX_settlements_guid",
                table: "settlements",
                column: "guid");

            migrationBuilder.CreateIndex(
                name: "IX_settlements_to_user_id_settlement_date",
                table: "settlements",
                columns: new[] { "to_user_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_expires_at",
                table: "two_factor_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_token_hash",
                table: "two_factor_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_token_type",
                table: "two_factor_tokens",
                column: "token_type");

            migrationBuilder.CreateIndex(
                name: "IX_two_factor_tokens_user_id",
                table: "two_factor_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_deleted_at",
                table: "users",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_guid",
                table: "users",
                column: "guid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_splits");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "imports");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "settlements");

            migrationBuilder.DropTable(
                name: "two_factor_tokens");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
