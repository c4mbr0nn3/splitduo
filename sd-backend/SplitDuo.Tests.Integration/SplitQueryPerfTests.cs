using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Recurring;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// Manual-only characterization of SingleQuery vs SplitQuery for the two recurring-expense
/// read shapes. This is a one-off decision tool, not a gate: nothing here asserts on timing.
///
/// Run explicitly:
///   ./run-integration-tests.sh --filter "Category=Perf"
/// The default integration run (and CI) excludes it with <c>Category!=Perf</c>.
///
/// What it reports per point: statements, materialized rows (must match across modes),
/// median + IQR wall-clock, with the measured round-trip time printed as the noise floor.
/// Freshly seeded tables have zero dead tuples and a hot buffer cache, so plans are
/// optimistic versus production steady state — the A/B comparison is still valid because
/// both arms see the same data.
/// </summary>
[Trait("Category", "Perf")]
public class SplitQueryPerfTests : IntegrationTest
{
    private const int WarmupIterations = 3;
    private const int MeasuredIterations = 10;

    private readonly ITestOutputHelper _output;

    public SplitQueryPerfTests(SplitDuoApiFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task RecurringQueries_SingleVsSplit_Characterization()
    {
        var points = new[]
        {
            // Realistic shapes: the API enforces alias-XOR-user splits, so one collection is empty.
            new PerfPoint("user-mode      ", Templates: 10, UserSplits: 10, AliasSplits: 0, Orphans: 0),
            new PerfPoint("alias-mode     ", Templates: 10, UserSplits: 0, AliasSplits: 10, Orphans: 0),
            // Non-production insurance: both collections populated, the shape the API cannot create.
            new PerfPoint("cross-product  ", Templates: 20, UserSplits: 50, AliasSplits: 50, Orphans: 0),
            // Crash-recovery path: steady state is zero orphans, so stress recovery sizes instead.
            new PerfPoint("recovery-1000  ", Templates: 100, UserSplits: 10, AliasSplits: 0, Orphans: 1000),
        };

        var databaseVersion = await GetPostgresVersionAsync();
        var rtt = await MeasureRoundTripAsync();
        _output.WriteLine($"PostgreSQL: {databaseVersion}");
        _output.WriteLine($"RTT baseline (SELECT 1, pooled): {rtt:F3} ms — wall-clock deltas below ~2x this are below the decision floor.");
        _output.WriteLine("Caveats: same-host Testcontainers (no WAN RTT), freshly seeded tables (zero dead tuples), hot buffer cache.");

        foreach (var point in points)
        {
            await Factory.ResetDatabaseAsync();
            await SeedAsync(point);
            await AnalyzeAsync();

            var today = JobToday;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(Factory.ConnectionString)
                .Options;

            var singleTemplates = await CaptureTemplatesAsync(false, today);
            var splitTemplates = await CaptureTemplatesAsync(true, today);
            var singleOrphans = await CaptureOrphansAsync(false);
            var splitOrphans = await CaptureOrphansAsync(true);

            Assert.Equal(singleTemplates.Rows, splitTemplates.Rows);
            Assert.Equal(singleOrphans.Rows, splitOrphans.Rows);

            _output.WriteLine(
                $"[{point.Name}] Q1 templates: stmts {singleTemplates.Statements}->{splitTemplates.Statements}, " +
                $"rows {singleTemplates.Rows}->{splitTemplates.Rows}");
            _output.WriteLine(
                $"[{point.Name}] Q2 orphans:   stmts {singleOrphans.Statements}->{splitOrphans.Statements}, " +
                $"rows {singleOrphans.Rows}->{splitOrphans.Rows}");

            for (var i = 0; i < WarmupIterations; i++)
            {
                await TimeOnceAsync(options, templates: true, split: false, today);
                await TimeOnceAsync(options, templates: true, split: true, today);
                await TimeOnceAsync(options, templates: false, split: false, today);
                await TimeOnceAsync(options, templates: false, split: true, today);
            }

            var templateSingle = new List<double>(MeasuredIterations);
            var templateSplit = new List<double>(MeasuredIterations);
            var orphanSingle = new List<double>(MeasuredIterations);
            var orphanSplit = new List<double>(MeasuredIterations);

            for (var i = 0; i < MeasuredIterations; i++)
            {
                // Alternate arm order to cancel buffer/planner drift between modes.
                var singleFirst = i % 2 == 0;

                var templatesPair = await MeasurePairAsync(options, templates: true, today, singleFirst);
                templateSingle.Add(templatesPair.single);
                templateSplit.Add(templatesPair.split);

                var orphansPair = await MeasurePairAsync(options, templates: false, today, singleFirst);
                orphanSingle.Add(orphansPair.single);
                orphanSplit.Add(orphansPair.split);
            }

            Report($"[{point.Name}] Q1 templates", templateSingle, templateSplit);
            Report($"[{point.Name}] Q2 orphans  ", orphanSingle, orphanSplit);

            // Drop tracked graphs so the next point seeds into a clean process state.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private void Report(string label, List<double> single, List<double> split)
    {
        var (singleMedian, singleIqr) = Statistics(single);
        var (splitMedian, splitIqr) = Statistics(split);
        var delta = splitMedian - singleMedian;
        _output.WriteLine(
            $"{label}: single med {singleMedian:F2} ms (IQR {singleIqr:F2}) | " +
            $"split med {splitMedian:F2} ms (IQR {splitIqr:F2}) | delta {delta:+0.00;-0.00;0.00} ms");
    }

    private async Task<(int Statements, long Rows)> CaptureTemplatesAsync(bool split, DateOnly today)
    {
        var counter = new StatementCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Factory.ConnectionString)
            .AddInterceptors(counter)
            .Options;

        await using var db = new AppDbContext(options);
        var templates = await FetchTemplatesAsync(db, split, today);
        return (counter.Count, RowCount(templates));
    }

    private async Task<(int Statements, long Rows)> CaptureOrphansAsync(bool split)
    {
        var counter = new StatementCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Factory.ConnectionString)
            .AddInterceptors(counter)
            .Options;

        await using var db = new AppDbContext(options);
        var instances = await FetchOrphansAsync(db, split);
        return (counter.Count, RowCount(instances));
    }

    private static async Task<List<RecurringExpenseTemplate>> FetchTemplatesAsync(
        AppDbContext db, bool split, DateOnly today)
    {
        var query = db.RecurringExpenseTemplates.DueTemplates(today);
        return split
            ? await query.AsSplitQuery().ToListAsync()
            : await query.AsSingleQuery().ToListAsync();
    }

    private static async Task<List<RecurringExpenseInstance>> FetchOrphansAsync(AppDbContext db, bool split)
    {
        var query = db.RecurringExpenseInstances.OrphanAutoPublishedInstances();
        return split
            ? await query.AsSplitQuery().ToListAsync()
            : await query.AsSingleQuery().ToListAsync();
    }

    private static long RowCount(List<RecurringExpenseTemplate> templates) =>
        templates.Count + templates.Sum(t => t.Splits.Count) + templates.Sum(t => t.AliasSplits.Count);

    private static long RowCount(List<RecurringExpenseInstance> instances) =>
        instances.Count
        + instances.Sum(i => i.Template.Splits.Count)
        + instances.Sum(i => i.Template.AliasSplits.Count);

    private static async Task<(double single, double split)> MeasurePairAsync(
        DbContextOptions<AppDbContext> options, bool templates, DateOnly today, bool singleFirst)
    {
        if (singleFirst)
        {
            var single = await TimeOnceAsync(options, templates, split: false, today);
            var split = await TimeOnceAsync(options, templates, split: true, today);
            return (single, split);
        }

        var splitFirst = await TimeOnceAsync(options, templates, split: true, today);
        var singleSecond = await TimeOnceAsync(options, templates, split: false, today);
        return (singleSecond, splitFirst);
    }

    private static async Task<double> TimeOnceAsync(
        DbContextOptions<AppDbContext> options, bool templates, bool split, DateOnly today)
    {
        // Fresh context per execution mirrors the job's scoped DbContext per run.
        await using var db = new AppDbContext(options);
        var stopwatch = Stopwatch.StartNew();

        if (templates)
        {
            await FetchTemplatesAsync(db, split, today);
        }
        else
        {
            await FetchOrphansAsync(db, split);
        }

        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private async Task SeedAsync(PerfPoint point)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(Factory.TimeProvider.GetUtcNow().UtcDateTime);

        var users = Enumerable.Range(0, Math.Max(1, point.UserSplits)).Select(i => new User
        {
            Email = $"perf-{point.Name.Trim()}-{i}@perf.local",
            PasswordHash = "x",
            FirstName = "Perf",
            LastName = "User",
            Settings = new UserSettings(),
        }).ToList();
        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        var group = new Group
        {
            Name = $"perf-{point.Name.Trim()}",
            CreatedBy = users[0].Id,
            UseAliases = point.AliasSplits > 0,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);

        var aliases = Enumerable.Range(0, point.AliasSplits).Select(i => new Alias
        {
            GroupId = group.Id,
            Name = $"perf-alias-{i}",
        }).ToList();
        db.Aliases.AddRange(aliases);
        await db.SaveChangesAsync(ct);

        var templates = new List<RecurringExpenseTemplate>(point.Templates);
        for (var t = 0; t < point.Templates; t++)
        {
            var template = new RecurringExpenseTemplate
            {
                GroupId = group.Id,
                OwnerId = users[0].Id,
                PaidByUserId = users[0].Id,
                Title = $"perf-template-{t}",
                Amount = 100m,
                CategoryId = 1,
                PaymentModeId = 1,
                RecurrenceModeId = (int)RecurrenceMode.Weekly,
                Weekdays = 127,
                AnchorDate = today.AddDays(-30),
                IsActive = true,
            };

            for (var s = 0; s < point.UserSplits; s++)
            {
                template.Splits.Add(new RecurringExpenseTemplateSplit
                {
                    UserId = users[s].Id,
                    SplitAmount = 100m / Math.Max(1, point.UserSplits),
                });
            }

            for (var s = 0; s < point.AliasSplits; s++)
            {
                template.AliasSplits.Add(new RecurringExpenseTemplateAliasSplit
                {
                    AliasId = aliases[s].Id,
                    SplitAmount = 100m / Math.Max(1, point.AliasSplits),
                });
            }

            templates.Add(template);
        }
        db.RecurringExpenseTemplates.AddRange(templates);
        await db.SaveChangesAsync(ct);

        if (point.Orphans == 0) return;

        var instances = new List<RecurringExpenseInstance>(point.Orphans);
        for (var i = 0; i < point.Orphans; i++)
        {
            var template = templates[i % templates.Count];
            // Distinct (TemplateId, PeriodDate) per template keeps the unique index happy.
            instances.Add(new RecurringExpenseInstance
            {
                TemplateId = template.Id,
                PeriodDate = today.AddDays(-(i / templates.Count) - 1),
                Amount = 100m,
                Status = RecurringExpenseInstanceStatus.AutoPublished,
            });
        }
        db.RecurringExpenseInstances.AddRange(instances);
        await db.SaveChangesAsync(ct);
    }

    private async Task AnalyzeAsync()
    {
        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "ANALYZE recurring_expense_templates, recurring_expense_template_splits, " +
            "recurring_expense_template_alias_splits, recurring_expense_instances, groups, users, aliases",
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> GetPostgresVersionAsync()
    {
        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SHOW server_version", conn);
        return (string?)await cmd.ExecuteScalarAsync() ?? "unknown";
    }

    private async Task<double> MeasureRoundTripAsync()
    {
        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync();

        const int samples = 100;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < samples; i++)
        {
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
        }
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds / samples;
    }

    private static (double median, double iqr) Statistics(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return (Percentile(sorted, 0.50), Percentile(sorted, 0.75) - Percentile(sorted, 0.25));
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var index = (sorted.Count - 1) * p;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
    }

    private sealed record PerfPoint(string Name, int Templates, int UserSplits, int AliasSplits, int Orphans);

    private sealed class StatementCountingInterceptor : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override DbDataReader ReaderExecuted(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Count++;
            return result;
        }

        // EF executes ToListAsync through the async ADO.NET path, so only the async
        // interceptor hook fires for these queries.
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
