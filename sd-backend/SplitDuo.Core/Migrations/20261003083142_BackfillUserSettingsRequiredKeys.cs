using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SplitDuo.Core.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUserSettingsRequiredKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core 10 materializes a required member of a ToJson() complex type as null when
            // its key is absent from the stored document, then throws NullRequiredComplexProperty
            // on save (dotnet/efcore#38625; fixed only in EF Core 12). Existing users.settings
            // rows predate Theme/UiLanguage/DismissedNotifications and may lack any of them.
            //
            // Right-biased merge: `settings` is the RIGHT operand, so real user values always win
            // and only missing keys are filled. Do NOT reverse the operands — `settings || defaults`
            // would overwrite real Theme/UiLanguage values.
            //
            // The jsonb_typeof guard is mandatory: `||` raises on a non-object document, which would
            // abort this startup migration (Program.cs runs Database.MigrateAsync) and prevent the
            // app from starting.
            migrationBuilder.Sql(
                """
                UPDATE users
                SET settings = '{"Theme":"auto","UiLanguage":"en","DismissedNotifications":[]}'::jsonb || settings
                WHERE jsonb_typeof(settings) = 'object';
                """);

            // A literal JSON null key is *present*, so the right-biased merge above leaves it in
            // place. Repair those explicitly. (jsonb_typeof(...) returns SQL NULL when the key is
            // missing, so these statements only match an explicit `null` value.)
            migrationBuilder.Sql(
                """
                UPDATE users
                SET settings = jsonb_set(settings, '{Theme}', '"auto"'::jsonb)
                WHERE jsonb_typeof(settings) = 'object' AND jsonb_typeof(settings->'Theme') = 'null';
                """);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET settings = jsonb_set(settings, '{UiLanguage}', '"en"'::jsonb)
                WHERE jsonb_typeof(settings) = 'object' AND jsonb_typeof(settings->'UiLanguage') = 'null';
                """);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET settings = jsonb_set(settings, '{DismissedNotifications}', '[]'::jsonb)
                WHERE jsonb_typeof(settings) = 'object' AND jsonb_typeof(settings->'DismissedNotifications') = 'null';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op. This migration is data-only: it changes no schema. A real Down
            // would have to remove the backfilled keys, but a backfilled `[]` is indistinguishable
            // from a user-authored empty list, so doing so would destroy genuine dismissal data.
            // The only consumers of these keys are builds that also contain this backfill, so
            // rolling the app back leaves the repaired data safe to ignore.
        }
    }
}
