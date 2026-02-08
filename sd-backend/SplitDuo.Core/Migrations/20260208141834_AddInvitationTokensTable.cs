using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationTokensTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitation_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guid = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    invited_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<long>(type: "bigint", nullable: false),
                    accepted_at = table.Column<long>(type: "bigint", nullable: true),
                    revoked_at = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invitation_tokens_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_email",
                table: "invitation_tokens",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_email_group_id",
                table: "invitation_tokens",
                columns: new[] { "email", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_group_id",
                table: "invitation_tokens",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_invited_by_user_id",
                table: "invitation_tokens",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_tokens_token_hash",
                table: "invitation_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_tokens");
        }
    }
}
