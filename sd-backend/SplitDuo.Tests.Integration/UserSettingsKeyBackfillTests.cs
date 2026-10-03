using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// Exercises the actual <c>BackfillUserSettingsRequiredKeys</c> migration via
/// <see cref="IMigrator"/>: downgrades to the previous migration, corrupts
/// users.settings into legacy shapes, proves the pre-backfill save bug, then
/// upgrades to latest and asserts the backfill repaired/preserved data.
/// </summary>
public class UserSettingsKeyBackfillTests : IntegrationTest
{
    public UserSettingsKeyBackfillTests(SplitDuoApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Backfill_FillsMissingKeys_PreservesUserValues_RecordsMigration()
    {
        var ct = TestContext.Current.CancellationToken;
        const string previousMigrationId = "20260927165643_AddAiCallLogFeatureAndLatency";
        const string backfillMigrationId = "20261003083142_BackfillUserSettingsRequiredKeys";

        // One scope stays open for the whole test: the migrator captures its scoped
        // DbContext, so it must not outlive the scope that created it.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Seed via EF so all NOT NULL columns are valid before we corrupt settings.
        var emptyEmail = await TestDbSeeder.SeedUserAsync(
            Factory.Services, "legacy-empty@localhost", "changeme123", "Legacy", "Empty");
        var partialEmail = await TestDbSeeder.SeedUserAsync(
            Factory.Services, "legacy-partial@localhost", "changeme123", "Legacy", "Partial");

        // 2. Downgrade to the migration before the backfill (runs its no-op Down).
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(previousMigrationId, ct);

        // 3. Raw-SQL (through EF's interpolator, so values are parameterized) mutate
        //    the seeded rows into their legacy shapes.
        await db.Database.ExecuteSqlAsync(
            $"UPDATE users SET settings = '{{}}'::jsonb WHERE email = {emptyEmail}");
        await db.Database.ExecuteSqlAsync(
            $"UPDATE users SET settings = '{{\"Theme\":\"dark\",\"UiLanguage\":\"de\"}}'::jsonb WHERE email = {partialEmail}");

        // 4. Prove the bug reproduces BEFORE backfill: loading a user whose settings
        //    document lacks required keys and saving it throws.
        {
            var user = await db.Users.FirstAsync(u => u.Email == emptyEmail, ct);
            user.FailedLoginAttempts = 1;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
            db.ChangeTracker.Clear();
        }

        // 5. Upgrade to latest — runs the real backfill migration.
        await migrator.MigrateAsync(cancellationToken: ct);

        // 6. Assert both rows have all three keys, non-null, with correct values.
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(ct);
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT email,
                       (settings ? 'Theme') AS has_theme,
                       (settings ? 'UiLanguage') AS has_ui_language,
                       (settings ? 'DismissedNotifications') AS has_dismissed,
                       jsonb_typeof(settings->'Theme') <> 'null' AS theme_not_null,
                       settings->>'Theme' AS theme,
                       settings->>'UiLanguage' AS ui_language,
                       settings->'DismissedNotifications' = '[]'::jsonb AS dismissed_empty,
                       jsonb_array_length(settings->'DismissedNotifications') AS dismissed_len
                FROM users
                WHERE email IN (@empty, @partial)
                """;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("empty", System.Data.DbType.String) { Value = emptyEmail });
            command.Parameters.Add(new Npgsql.NpgsqlParameter("partial", System.Data.DbType.String) { Value = partialEmail });

            var rows = new Dictionary<string, (bool HasTheme, bool HasUi, bool HasDismissed, bool ThemeNotNull,
                string? Theme, string? UiLanguage, bool DismissedEmpty, int DismissedLen)>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var email = reader.GetString(0);
                rows[email] = (
                    reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetBoolean(7), reader.GetInt32(8));
            }

            Assert.Equal(2, rows.Count);

            var empty = rows[emptyEmail];
            Assert.True(empty.HasTheme);
            Assert.True(empty.HasUi);
            Assert.True(empty.HasDismissed);
            Assert.True(empty.ThemeNotNull);
            Assert.Equal("auto", empty.Theme);          // default backfilled
            Assert.Equal("en", empty.UiLanguage);       // default backfilled
            Assert.True(empty.DismissedEmpty);
            Assert.Equal(0, empty.DismissedLen);

            var partial = rows[partialEmail];
            Assert.True(partial.HasTheme);
            Assert.True(partial.HasUi);
            Assert.True(partial.HasDismissed);
            Assert.True(partial.ThemeNotNull);
            Assert.Equal("dark", partial.Theme);        // preserved (right-biased merge)
            Assert.Equal("de", partial.UiLanguage);     // preserved (right-biased merge)
            Assert.True(partial.DismissedEmpty);
            Assert.Equal(0, partial.DismissedLen);
        }

        // 7. Optional literal-null repair: a present-but-null Theme becomes "auto"
        //    (the right-biased merge skips present keys; the jsonb_set repairs them).
        {
            var nullEmail = await TestDbSeeder.SeedUserAsync(
                Factory.Services, "legacy-null@localhost", "changeme123", "Legacy", "Null");

            // Downgrade again (no-op Down), corrupt with a literal null, upgrade again.
            await migrator.MigrateAsync(previousMigrationId, ct);
            await db.Database.ExecuteSqlAsync(
                $"UPDATE users SET settings = '{{\"Theme\":null}}'::jsonb WHERE email = {nullEmail}");
            await migrator.MigrateAsync(cancellationToken: ct);

            var repaired = await db.Users
                .Where(u => u.Email == nullEmail)
                .Select(u => u.Settings.Theme)
                .FirstAsync(ct);
            Assert.Equal("auto", repaired);
        }

        // 8. Migration is recorded in the history table.
        var recorded = await db.Database
            .SqlQuery<string?>($"""
                SELECT "MigrationId" AS "Value" FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = {backfillMigrationId}
                """)
            .FirstOrDefaultAsync(ct);
        Assert.Equal(backfillMigrationId, recorded);
    }
}