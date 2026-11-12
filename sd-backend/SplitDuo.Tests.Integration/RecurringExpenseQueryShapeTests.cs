using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Recurring;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// Deterministic guard for the two recurring-expense read shapes. It fails if a future
/// edit drops <c>AsSplitQuery()</c> (the query then falls back to SingleQuery and hits
/// EF Core event 20504) or if a new multi-collection include is added to these shapes
/// without choosing a splitting behavior.
///
/// Deliberately timing-free: this runs in the normal integration gate in milliseconds.
/// </summary>
public class RecurringExpenseQueryShapeTests : IntegrationTest
{
    private const int MinimumSplitStatements = 2;

    public RecurringExpenseQueryShapeTests(SplitDuoApiFactory factory) : base(factory) { }

    [Fact]
    public async Task JobQueries_UseSplitQuery_AndDoNotTriggerMultipleCollectionIncludeWarning()
    {
        await SeedDueTemplateWithOrphanAsync();

        var recorder = new StatementCountingInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Factory.ConnectionString)
            .AddInterceptors(recorder)
            // The warning is the regression detector: a single-query multi-collection
            // include now fails this test instead of silently degrading to a cartesian join.
            .ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning))
            .Options;

        var today = JobToday;

        await using (var db = new AppDbContext(options))
        {
            recorder.Reset();
            var templates = await db.RecurringExpenseTemplates
                .DueTemplates(today)
                .AsSplitQuery()
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(templates);
            Assert.True(
                recorder.Count >= MinimumSplitStatements,
                $"DueTemplates should execute as a split query (>= {MinimumSplitStatements} statements) " +
                $"but issued {recorder.Count}.");
        }

        await using (var db = new AppDbContext(options))
        {
            recorder.Reset();
            var orphans = await db.RecurringExpenseInstances
                .OrphanAutoPublishedInstances()
                .AsSplitQuery()
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(orphans);
            Assert.True(
                recorder.Count >= MinimumSplitStatements,
                $"OrphanAutoPublishedInstances should execute as a split query (>= {MinimumSplitStatements} statements) " +
                $"but issued {recorder.Count}.");
        }
    }

    private async Task SeedDueTemplateWithOrphanAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var today = DateOnly.FromDateTime(Factory.TimeProvider.GetUtcNow().UtcDateTime);

        var user = new User
        {
            Email = "query-shape@perf.local",
            PasswordHash = "x",
            FirstName = "Query",
            LastName = "Shape",
            Settings = new UserSettings(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var group = new Group { Name = "query-shape-group", CreatedBy = user.Id };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);

        var template = new RecurringExpenseTemplate
        {
            GroupId = group.Id,
            OwnerId = user.Id,
            PaidByUserId = user.Id,
            Title = "query-shape-template",
            Amount = 10m,
            CategoryId = 1,
            PaymentModeId = 1,
            RecurrenceModeId = (int)RecurrenceMode.Weekly,
            Weekdays = 127,
            AnchorDate = today.AddDays(-30),
            IsActive = true,
        };
        db.RecurringExpenseTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        db.RecurringExpenseTemplateSplits.Add(new RecurringExpenseTemplateSplit
        {
            TemplateId = template.Id,
            UserId = user.Id,
            SplitAmount = 10m,
        });
        db.RecurringExpenseInstances.Add(new RecurringExpenseInstance
        {
            TemplateId = template.Id,
            PeriodDate = today.AddDays(-1),
            Amount = 10m,
            Status = RecurringExpenseInstanceStatus.AutoPublished,
        });
        await db.SaveChangesAsync(ct);
    }

    private sealed class StatementCountingInterceptor : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public void Reset() => Count = 0;

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
