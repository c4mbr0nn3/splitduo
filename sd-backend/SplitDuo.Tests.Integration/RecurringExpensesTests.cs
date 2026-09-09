using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.RecurringExpenses.Dto;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;
using SplitDuo.Core.Services.Expenses;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class RecurringExpensesTests : IntegrationTest
{
    public RecurringExpensesTests(SplitDuoApiFactory factory) : base(factory) { }

    // --- Job execution helper: direct construction (job has NO other coverage) ---

    /// <summary>
    /// Constructs RecurringExpenseGenerationJob directly from the DI container and
    /// executes it. The job never reads IJobExecutionContext (only the logger/today
    /// from TimeProvider), so null is safe — verified against the job source.
    /// </summary>
    private async Task RunGenerationJobAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var job = new RecurringExpenseGenerationJob(
            sp.GetRequiredService<ILogger<RecurringExpenseGenerationJob>>(),
            sp.GetRequiredService<IUnitOfWork>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ICacheInvalidator>(),
            sp.GetRequiredService<IExpenseCreationService>());
        await job.Execute(null!);
    }

    // --- Payload helpers ---

    private static string TodayIso => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    private static int WeekdayBitForToday()
    {
        var dow = DateTime.UtcNow.DayOfWeek;
        return 1 << ((int)dow == 0 ? 6 : (int)dow - 1);
    }

    private static object TemplatePayload(
        string paidByUserId, object splits, bool requiresApproval,
        string? anchorDate = null, int recurrenceMode = (int)RecurrenceMode.Weekly,
        int? dayOfMonth = null, int? weekdays = null, int? interval = null) => new
    {
        title = "Recurring Test",
        amount = 100m,
        categoryId = 1,
        paymentModeId = 1,
        paidByUserId,
        recurrenceMode,
        weekdays = weekdays ?? (recurrenceMode == (int)RecurrenceMode.Weekly ? WeekdayBitForToday() : 0),
        dayOfMonth,
        interval,
        anchorDate = anchorDate ?? TodayIso,
        requiresApproval,
        splits,
    };

    private async Task<RecurringExpenseTemplateDto> CreateTemplateAsync(
        HttpClient client, string groupId, string paidByUserId, object splits,
        bool requiresApproval = false, string? anchorDate = null,
        int recurrenceMode = (int)RecurrenceMode.Weekly,
        int? dayOfMonth = null, int? weekdays = null, int? interval = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/recurring-expenses",
            TemplatePayload(paidByUserId, splits, requiresApproval, anchorDate, recurrenceMode, dayOfMonth, weekdays, interval), ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        return body!.Data!;
    }

    private async Task<List<RecurringExpenseInstanceDto>> GetInstancesAsync(HttpClient client, string groupId, string? status = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var url = $"/api/v1/groups/{groupId}/recurring-expenses/instances";
        if (status != null) url += $"?status={status}";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<RecurringExpenseInstanceDto>>(ct);
        return body!.Data!;
    }

    private async Task<(int expenses, int instances)> CountRecurringRowsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenses = await db.Expenses.CountAsync(e => e.RecurringExpenseTemplateId != null);
        var instances = await db.RecurringExpenseInstances.CountAsync();
        return (expenses, instances);
    }

    private static int WeekdayBitFor(DateOnly d)
    {
        var dow = d.DayOfWeek;
        return 1 << ((int)dow == 0 ? 6 : (int)dow - 1);
    }

    private static DateOnly NextFriday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysAhead = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
        if (daysAhead == 0) daysAhead = 7; // strictly in the future
        return today.AddDays(daysAhead);
    }

    private static string NextFridayIso => NextFriday().ToString("yyyy-MM-dd");

    /// <summary>
    /// Seeds an AutoPublished instance WITHOUT its expense (simulates the crash between
    /// GenerateForPeriodAsync's instance commit and the expense commit). Seeded via a
    /// fresh DI scope so the row is committed and visible to the job's own scope.
    /// </summary>
    private async Task SeedOrphanAutoPublishedInstanceAsync(string templateGuid, DateOnly periodDate)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = await db.RecurringExpenseTemplates
            .Include(t => t.Splits)
            .FirstAsync(t => t.Guid.ToString() == templateGuid);

        var instance = new RecurringExpenseInstance
        {
            TemplateId = template.Id,
            PeriodDate = periodDate,
            Amount = template.Amount,
            Status = RecurringExpenseInstanceStatus.AutoPublished, // [NotMapped] Status sets StatusId
            ExpenseId = null,
        };
        db.RecurringExpenseInstances.Add(instance);
        await db.SaveChangesAsync(); // instance committed first — mirrors production ordering

        foreach (var split in template.Splits)
        {
            db.RecurringExpenseInstanceSplits.Add(new RecurringExpenseInstanceSplit
            {
                InstanceId = instance.Id,
                UserId = split.UserId,
                SplitAmount = split.SplitAmount,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Fetches the Expense row created for a template (by template Guid) directly from the DB.</summary>
    private async Task<Core.Domain.Entities.Expense?> GetTemplateExpenseAsync(string templateGuid)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.RecurringExpenseTemplates.FirstAsync(t => t.Guid.ToString() == templateGuid);
        return await db.Expenses.FirstOrDefaultAsync(e => e.RecurringExpenseTemplateId == template.Id);
    }

    // --- 1. Auto-publish generation + idempotency ---

    [Fact]
    public async Task Job_AutoPublish_CreatesOneExpenseWithInstanceLinked_AndIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        await RunGenerationJobAsync();

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(1, instances);

        // AutoPublished instance (status 4) with ExpenseId set
        var instanceList = await GetInstancesAsync(client, group.Id);
        var instance = Assert.Single(instanceList);
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, instance.Status);
        Assert.Equal(template.Id, instance.TemplateId);
        Assert.NotNull(instance.ExpenseId);

        // Expense fields: ExpenseDate = expected occurrence (anchor = today, weekly on today's weekday)
        var expense = await GetTemplateExpenseAsync(template.Id);
        Assert.NotNull(expense);
        Assert.Equal(TodayIso, expense!.ExpenseDate.ToString("yyyy-MM-dd"));

        // Expense appears via API with correct date
        var expenseResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Guid}", ct);
        Assert.Equal(HttpStatusCode.OK, expenseResponse.StatusCode);
        var expenseBody = await expenseResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal(TodayIso, expenseBody!.Data!.ExpenseDate);

        // Idempotency: second run must not create anything new
        await RunGenerationJobAsync();
        var (expenses2, instances2) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses2);
        Assert.Equal(1, instances2);
    }

    // --- 2. Pending-approval path ---

    [Fact]
    public async Task Job_RequiresApproval_CreatesPendingInstance_AndNoExpense()
    {
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, requiresApproval: true);

        await RunGenerationJobAsync();

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(0, expenses);
        Assert.Equal(1, instances);

        var instanceList = await GetInstancesAsync(client, group.Id, "PendingApproval");
        var instance = Assert.Single(instanceList);
        Assert.Equal((int)RecurringExpenseInstanceStatus.PendingApproval, instance.Status);
        Assert.Null(instance.ExpenseId);
    }

    // --- 3. Approve flow ---

    [Fact]
    public async Task ApproveInstance_WithEditedAmountAndSplits_CreatesExpense_AndUpdatesBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, requiresApproval: true);

        await RunGenerationJobAsync();

        var instance = Assert.Single(await GetInstancesAsync(client, group.Id, "PendingApproval"));

        // Approve with EDITED amount + matching edited splits
        var approveResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/instances/{instance.Id}/approve",
            new
            {
                amount = 80m,
                splits = new[]
                {
                    new { userId = admin.Id, splitAmount = 50m },
                    new { userId = user2Id, splitAmount = 30m },
                },
            }, ct);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal(80m, approved!.Data!.Amount);
        Assert.Equal(2, approved.Data.Splits.Count);

        // Instance is Approved with approvedBy + expenseId set
        var approvedInstances = await GetInstancesAsync(client, group.Id, "Approved");
        var approvedInstance = Assert.Single(approvedInstances);
        Assert.Equal((int)RecurringExpenseInstanceStatus.Approved, approvedInstance.Status);
        Assert.Equal(admin.Id, approvedInstance.ApprovedBy);
        Assert.NotNull(approvedInstance.ExpenseId);
        Assert.NotNull(approvedInstance.ApprovedAt);

        // Expense persisted with the EDITED amount
        var (expenses, _) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);

        // Balances reflect the new expense: admin paid 80, owed 50 → +30; user2 owes 30
        var balancesResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);
        balancesResponse.EnsureSuccessStatusCode();
        var balances = await balancesResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);
        var adminBalance = balances!.Data!.Single(b => b.UserId == admin.Id);
        var user2Balance = balances.Data.Single(b => b.UserId == user2Id);
        Assert.Equal(80m, adminBalance.TotalPaid);
        Assert.Equal(50m, adminBalance.TotalOwed);
        Assert.Equal(30m, adminBalance.Balance);
        Assert.Equal(30m, user2Balance.TotalOwed);
        Assert.Equal(-30m, user2Balance.Balance);
    }

    // --- 4. Approve/authz guards ---

    [Fact]
    public async Task ApproveInstance_PlainMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, _, memberClient) = await SeedSecondMemberAsync(client, group.Id);

        await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, requiresApproval: true);
        await RunGenerationJobAsync();

        var instance = Assert.Single(await GetInstancesAsync(client, group.Id, "PendingApproval"));

        var response = await memberClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/instances/{instance.Id}/approve",
            new { amount = 100m }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplates_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        // Seed a user who is NOT a member of the group
        await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost", "changeme123", "Out", "Sider");
        var outsiderClient = await CreateAuthenticatedClientAsync("outsider@localhost", "changeme123");

        var response = await outsiderClient.GetAsync($"/api/v1/groups/{group.Id}/recurring-expenses", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTemplate_PlainMemberUpdatingOwnersTemplate_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, _, memberClient) = await SeedSecondMemberAsync(client, group.Id);

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        var response = await memberClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}",
            new
            {
                title = "Hijacked",
                amount = 1m,
                categoryId = 1,
                paymentModeId = 1,
                paidByUserId = admin.Id,
                recurrenceMode = (int)RecurrenceMode.Weekly,
                weekdays = WeekdayBitForToday(),
                anchorDate = TodayIso,
                splits = new[] { new { userId = admin.Id, splitAmount = 1m } },
            }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Template unchanged
        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}", ct);
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.Equal("Recurring Test", body!.Data!.Title);
    }

    // --- 5. Reject flow + no regeneration ---

    [Fact]
    public async Task RejectInstance_MarksRejected_AndJobDoesNotRegeneratePeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } }, requiresApproval: true);

        await RunGenerationJobAsync();

        var instance = Assert.Single(await GetInstancesAsync(client, group.Id, "PendingApproval"));

        var rejectResponse = await client.PostAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/instances/{instance.Id}/reject", null, ct);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var rejected = Assert.Single(await GetInstancesAsync(client, group.Id, "Rejected"));
        Assert.Equal((int)RecurringExpenseInstanceStatus.Rejected, rejected.Status);

        // Re-run job: the rejected (templateId, periodDate) must NOT be regenerated
        await RunGenerationJobAsync();

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(0, expenses);
        Assert.Equal(1, instances);
        Assert.Empty(await GetInstancesAsync(client, group.Id, "PendingApproval"));
        Assert.Single(await GetInstancesAsync(client, group.Id, "Rejected"));
    }

    // --- 6. Auto-pause on invalid template ---

    [Fact]
    public async Task Job_PayerRemovedFromGroup_PausesTemplateWithReason_AndWritesNoExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // Payer = user2, who will be removed from the group afterwards
        await CreateTemplateAsync(client, group.Id, user2Id,
            new[] { new { userId = user2Id, splitAmount = 100m } });

        var removeResponse = await client.DeleteAsync($"/api/v1/groups/{group.Id}/members/{user2Id}", ct);
        Assert.True(removeResponse.IsSuccessStatusCode,
            $"member removal failed: {removeResponse.StatusCode}");

        await RunGenerationJobAsync();

        // Template paused with reason; no expense written
        var templatesResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/recurring-expenses?includeInactive=true", ct);
        templatesResponse.EnsureSuccessStatusCode();
        var templates = await templatesResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<RecurringExpenseTemplateDto>>>(ct);
        var paused = Assert.Single(templates!.Data!);
        Assert.False(paused.IsActive);
        Assert.NotNull(paused.PausedReason);
        Assert.NotEmpty(paused.PausedReason!);

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(0, expenses);
        // The instance row itself IS written (instance-first ordering) — the
        // contract under test is: no broken expense persisted + template paused.
        Assert.Equal(1, instances);
        Assert.Null((await GetInstancesAsync(client, group.Id))[0].ExpenseId);
    }

    // --- 7. Preview endpoint ---

    [Fact]
    public async Task Preview_MonthlyDay15_ReturnsNextThreeOccurrences()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/preview",
            new
            {
                recurrenceMode = (int)RecurrenceMode.Monthly,
                weekdays = 0,
                dayOfMonth = 15,
                anchorDate = TodayIso,
            }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurrencePreviewResponseDto>>(ct);
        Assert.Equal(3, body!.Data!.NextOccurrences.Count);

        // Expected: the 15th of each month, starting from the first 15th >= today
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = new DateOnly(today.Year, today.Month, 15);
        if (first < today) first = first.AddMonths(1);
        var expected = new[]
        {
            first.ToString("yyyy-MM-dd"),
            first.AddMonths(1).ToString("yyyy-MM-dd"),
            first.AddMonths(2).ToString("yyyy-MM-dd"),
        };
        Assert.Equal(expected, body.Data.NextOccurrences);
        Assert.Contains("Monthly on the 15th", body.Data.Summary);
    }

    // --- 8. Orphan reconciliation, toggle-active, edit series ---

    [Fact]
    public async Task Job_Reconciliation_CompletesOrphanAutoPublishedInstance_WithoutRegeneratingPeriod()
    {
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        // Simulate the crash between the instance commit and the expense commit:
        // AutoPublished instance with ExpenseId == null already in the DB.
        await SeedOrphanAutoPublishedInstanceAsync(template.Id, DateOnly.FromDateTime(DateTime.UtcNow));

        await RunGenerationJobAsync();

        // The orphan was REUSED (no duplicates) and completed.
        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(1, instances);

        var instanceList = await GetInstancesAsync(client, group.Id);
        var instance = Assert.Single(instanceList);
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, instance.Status);
        Assert.NotNull(instance.ExpenseId);
        Assert.Equal(TodayIso, instance.PeriodDate);

        var expense = await GetTemplateExpenseAsync(template.Id);
        Assert.NotNull(expense);
        Assert.Equal(TodayIso, expense!.ExpenseDate.ToString("yyyy-MM-dd"));
        Assert.Equal(100m, expense.Amount);

        // Idempotency: second run must not create anything new (orphan no longer
        // matches the reconciliation filter since ExpenseId != null).
        await RunGenerationJobAsync();
        var (expenses2, instances2) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses2);
        Assert.Equal(1, instances2);
    }

    [Fact]
    public async Task Job_Reconciliation_FailingOrphan_PausesItsTemplate_ButOtherOrphansAreStillProcessed()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // Group A: healthy orphan (payer = admin)
        var groupA = await client.CreateGroupAsync(name: "GroupA");
        var admin = await client.GetCurrentUserAsync();
        var templateA = await CreateTemplateAsync(client, groupA.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });
        await SeedOrphanAutoPublishedInstanceAsync(templateA.Id, DateOnly.FromDateTime(DateTime.UtcNow));

        // Group B: orphan whose template payer will be removed from the group
        var groupB = await client.CreateGroupAsync(name: "GroupB");
        var (_, user2IdB, _) = await SeedSecondMemberAsync(client, groupB.Id);
        var templateB = await CreateTemplateAsync(client, groupB.Id, user2IdB,
            new[] { new { userId = user2IdB, splitAmount = 100m } });
        await SeedOrphanAutoPublishedInstanceAsync(templateB.Id, DateOnly.FromDateTime(DateTime.UtcNow));
        var removeResponse = await client.DeleteAsync($"/api/v1/groups/{groupB.Id}/members/{user2IdB}", ct);
        Assert.True(removeResponse.IsSuccessStatusCode,
            $"member removal failed: {removeResponse.StatusCode}");

        await RunGenerationJobAsync();

        // Group A: orphan completed
        var instancesA = await GetInstancesAsync(client, groupA.Id);
        var instanceA = Assert.Single(instancesA);
        Assert.NotNull(instanceA.ExpenseId);
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, instanceA.Status);

        // Group B: template paused with reason; instance row still orphaned
        var templatesResponse = await client.GetAsync($"/api/v1/groups/{groupB.Id}/recurring-expenses?includeInactive=true", ct);
        templatesResponse.EnsureSuccessStatusCode();
        var templates = await templatesResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<RecurringExpenseTemplateDto>>>(ct);
        var paused = Assert.Single(templates!.Data!);
        Assert.False(paused.IsActive);
        Assert.NotNull(paused.PausedReason);
        Assert.NotEmpty(paused.PausedReason!);

        var instancesB = await GetInstancesAsync(client, groupB.Id);
        var instanceB = Assert.Single(instancesB);
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, instanceB.Status);
        Assert.Null(instanceB.ExpenseId);

        // Negative: B's pause did not prevent A's reconciliation — exactly one expense total
        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(2, instances);
    }

    [Fact]
    public async Task ToggleActive_PauseThenResume_StopsGeneration_ResumesCleanly_WithoutDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // 1. Pause: IsActive = false, no PausedReason set by the toggle itself
        var pauseResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/toggle-active",
            new { isActive = false }, ct);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);
        var paused = await pauseResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.False(paused!.Data!.IsActive);
        Assert.Null(paused.Data.PausedReason);

        // Confirm persisted
        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/recurring-expenses?includeInactive=true", ct);
        getResponse.EnsureSuccessStatusCode();
        var templates = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<RecurringExpenseTemplateDto>>>(ct);
        var persisted = Assert.Single(templates!.Data!);
        Assert.False(persisted.IsActive);
        Assert.Null(persisted.PausedReason);

        // 2. Job while paused: no new rows (template query filters IsActive)
        await RunGenerationJobAsync();
        var (pausedExpenses, pausedInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, pausedExpenses);
        Assert.Equal(1, pausedInstances);

        // 3. Resume via legacy toggle: back-compat DEFAULT = skip. The fake clock has
        // NOT advanced, so 0 periods are missed — skip sets ResumeFrom to the first
        // future occurrence harmlessly (chosen + asserted behavior; plan Task 5).
        var resumeResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/toggle-active",
            new { isActive = true }, ct);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        var resumed = await resumeResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.True(resumed!.Data!.IsActive);
        Assert.Null(resumed.Data.PausedReason);
        Assert.Equal(JobToday.AddDays(7).ToString("yyyy-MM-dd"), resumed.Data.ResumeFrom);
        Assert.Equal(0, resumed.Data.MissedCount);

        // 4. Job after resume: already-materialized period NOT duplicated
        await RunGenerationJobAsync();
        var (finalExpenses, finalInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, finalExpenses);
        Assert.Equal(1, finalInstances);
        Assert.Single(await GetInstancesAsync(client, group.Id));
    }

    [Fact]
    public async Task UpdateTemplate_AfterGeneration_PastInstancesFrozen_FutureUsesNewSpec()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } });

        await RunGenerationJobAsync();

        // Capture the frozen baseline (instance + splits + expense) before editing
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templateEntity = await db.RecurringExpenseTemplates
                .FirstAsync(t => t.Guid.ToString() == template.Id);
            var oldInstance = await db.RecurringExpenseInstances
                .FirstAsync(i => i.TemplateId == templateEntity.Id);
            var oldSplits = await db.RecurringExpenseInstanceSplits
                .Where(s => s.InstanceId == oldInstance.Id).ToListAsync();
            var oldExpense = await db.Expenses
                .FirstAsync(e => e.RecurringExpenseTemplateId == templateEntity.Id);

            Assert.Equal(100m, oldInstance.Amount);
            var oldSplit = Assert.Single(oldSplits);
            Assert.Equal(100m, oldSplit.SplitAmount);
            Assert.Equal(100m, oldExpense.Amount);
        }

        var nextFriday = NextFriday();
        var nextFridayIso = nextFriday.ToString("yyyy-MM-dd");

        var editResponse = await client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}",
            new
            {
                title = "Edited",
                amount = 60m,
                categoryId = 1,
                paymentModeId = 1,
                paidByUserId = admin.Id,
                recurrenceMode = (int)RecurrenceMode.Weekly,
                weekdays = WeekdayBitFor(nextFriday),
                anchorDate = nextFridayIso,
                splits = new[]
                {
                    new { userId = admin.Id, splitAmount = 30m },
                    new { userId = user2Id, splitAmount = 30m },
                },
            }, ct);
        Assert.True(editResponse.IsSuccessStatusCode, $"edit failed: {editResponse.StatusCode}");
        var edited = await editResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.Equal("Edited", edited!.Data!.Title);
        Assert.Equal(60m, edited.Data.Amount);
        Assert.Equal(nextFridayIso, edited.Data.AnchorDate);

        await RunGenerationJobAsync();

        // NOTE: the job only generates occurrences within [AnchorDate, today]
        // (RecurrenceEvaluator.GetOccurrences returns [] when start > to). Because the
        // edited anchor is in the FUTURE (next Friday) and TimeProvider.System cannot be
        // advanced in the test host, the job generates ZERO new instances after the edit.
        // The test therefore verifies the plan's core guarantee — past instances/expenses
        // are frozen and the template carries the new spec — and asserts the job stays
        // idempotent (no third instance for TodayIso, nothing re-generated).

        // Past frozen: original instance unchanged, nothing re-generated
        var instances = await GetInstancesAsync(client, group.Id);
        var pastInstance = Assert.Single(instances);
        Assert.Equal(TodayIso, pastInstance.PeriodDate);
        Assert.Equal(100m, pastInstance.Amount);
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, pastInstance.Status);
        Assert.NotNull(pastInstance.ExpenseId);
        Assert.Single(pastInstance.Splits);
        Assert.Equal(100m, pastInstance.Splits[0].SplitAmount);

        // Negative: no duplicates, no future-dated row (job cannot reach the future anchor)
        var (expenses, dbInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(1, dbInstances);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templateEntity = await db.RecurringExpenseTemplates
                .Include(t => t.Splits)
                .FirstAsync(t => t.Guid.ToString() == template.Id);

            // Template carries the NEW spec after edit
            Assert.Equal("Edited", templateEntity.Title);
            Assert.Equal(60m, templateEntity.Amount);
            Assert.Equal(nextFriday, templateEntity.AnchorDate);
            Assert.Equal(WeekdayBitFor(nextFriday), templateEntity.Weekdays);

            // Template splits replaced with the NEW snapshot: [admin:30, user2:30]
            var templateSplits = templateEntity.Splits.ToList();
            Assert.Equal(2, templateSplits.Count);
            var templateSplitUsers = await db.Users
                .Where(u => templateSplits.Select(s => s.UserId).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Guid.ToString());
            Assert.Contains(templateSplits, s => templateSplitUsers[s.UserId] == admin.Id && s.SplitAmount == 30m);
            Assert.Contains(templateSplits, s => templateSplitUsers[s.UserId] == user2Id && s.SplitAmount == 30m);

            // Past expense untouched
            var pastExpense = await db.Expenses
                .FirstAsync(e => e.RecurringExpenseTemplateId == templateEntity.Id
                                 && e.ExpenseDate == DateOnly.FromDateTime(DateTime.UtcNow));
            Assert.Equal(100m, pastExpense.Amount);

            // Past instance splits untouched (still one admin:100 row)
            var pastInstanceEntity = await db.RecurringExpenseInstances
                .FirstAsync(i => i.TemplateId == templateEntity.Id
                                 && i.PeriodDate == DateOnly.FromDateTime(DateTime.UtcNow));
            var pastSplits = await db.RecurringExpenseInstanceSplits
                .Where(s => s.InstanceId == pastInstanceEntity.Id).ToListAsync();
            var pastSplit = Assert.Single(pastSplits);
            Assert.Equal(100m, pastSplit.SplitAmount);
        }

        // Template DTO: NextOccurrence is the first occurrence strictly after today
        Assert.Equal(nextFridayIso, edited.Data.NextOccurrence);
    }

    // --- 9. Time-dependent (FakeTimeProvider) ---

    [Fact]
    public async Task Job_PauseAdvanceThreeWeeks_ResumeBackfill_GeneratesAllMissedPeriods_WithoutDuplicates()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        // All expected dates derive from the FAKE clock (fake == real after reset).
        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // 1. Pause
        var pauseResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/toggle-active",
            new { isActive = false }, ct);
        Assert.True(pauseResponse.IsSuccessStatusCode);

        // 2. Advance 3 weeks; suppressed while paused (IsActive query filter)
        AdvanceTime(TimeSpan.FromDays(21));
        Assert.Equal(p0.AddDays(21), JobToday);
        await RunGenerationJobAsync();
        var (pausedExpenses, pausedInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, pausedExpenses);
        Assert.Equal(1, pausedInstances);

        // Paused DTO surfaces the 3 missed occurrences (p0+7, p0+14, p0+21; p0 already exists)
        var pausedBody = await pauseResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.Equal(0, pausedBody!.Data!.MissedCount); // at pause time nothing is missed yet
        var pausedNow = await client.GetAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}", ct);
        pausedNow.EnsureSuccessStatusCode();
        var pausedNowBody = await pausedNow.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.Equal(3, pausedNowBody!.Data!.MissedCount);
        Assert.Null(pausedNowBody.Data.ResumeFrom);

        // 3. Resume via /resume with strategy=backfill (explicit opt-in)
        var resumeResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/resume",
            new { strategy = "backfill" }, ct);
        Assert.True(resumeResponse.IsSuccessStatusCode);
        var resumedBody = await resumeResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.True(resumedBody!.Data!.IsActive);
        Assert.Null(resumedBody.Data.ResumeFrom);
        Assert.Equal(0, resumedBody.Data.MissedCount);

        // 4. Backfill: window [AnchorDate, today] enumerates all missed periods in one pass
        await RunGenerationJobAsync();

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(4, expenses);
        Assert.Equal(4, instances);

        var instanceList = await GetInstancesAsync(client, group.Id);
        Assert.Equal(4, instanceList.Count);
        var periodDates = instanceList.Select(i => i.PeriodDate).OrderBy(d => d).ToList();
        Assert.Equal(new[]
        {
            p0.ToString("yyyy-MM-dd"),
            p0.AddDays(7).ToString("yyyy-MM-dd"),
            p0.AddDays(14).ToString("yyyy-MM-dd"),
            p0.AddDays(21).ToString("yyyy-MM-dd"),
        }, periodDates);

        foreach (var instance in instanceList)
        {
            Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, instance.Status);
            Assert.NotNull(instance.ExpenseId);
        }

        // Each expense: Amount 100
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templateEntity = await db.RecurringExpenseTemplates
                .FirstAsync(t => t.Guid.ToString() == template.Id);
            var createdExpenses = await db.Expenses
                .Where(e => e.RecurringExpenseTemplateId == templateEntity.Id)
                .ToListAsync();
            Assert.Equal(4, createdExpenses.Count);
            foreach (var expense in createdExpenses)
            {
                Assert.Equal(100m, expense.Amount);
            }
        }

        // Negative: idempotent — no 5th instance on a second run
        await RunGenerationJobAsync();
        var (finalExpenses, finalInstances) = await CountRecurringRowsAsync();
        Assert.Equal(4, finalExpenses);
        Assert.Equal(4, finalInstances);
    }

    [Fact]
    public async Task Job_PauseAdvanceThreeWeeks_ResumeSkip_NeverGeneratesMissedPeriods()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // Pause + advance 3 weeks
        var pauseResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/toggle-active",
            new { isActive = false }, ct);
        Assert.True(pauseResponse.IsSuccessStatusCode);
        AdvanceTime(TimeSpan.FromDays(21));
        await RunGenerationJobAsync();
        var (pausedExpenses, pausedInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, pausedExpenses);
        Assert.Equal(1, pausedInstances);

        // Resume via /resume with strategy=skip
        var resumeResponse = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}/resume",
            new { strategy = "skip" }, ct);
        Assert.True(resumeResponse.IsSuccessStatusCode);
        var resumedBody = await resumeResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.True(resumedBody!.Data!.IsActive);
        Assert.Null(resumedBody.Data.PausedReason);
        Assert.Equal(p0.AddDays(28).ToString("yyyy-MM-dd"), resumedBody.Data.ResumeFrom);

        // Job: the 3 missed never appear. NOTE: skip sets the boundary to p0+28, which
        // is strictly AFTER fake-today (p0+21), so the window [p0+28, p0+21] is empty —
        // the forward period materializes only when the clock reaches p0+28.
        await RunGenerationJobAsync();

        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(1, instances);

        var instanceList = await GetInstancesAsync(client, group.Id);
        var pastInstance = Assert.Single(instanceList);
        Assert.Equal(p0.ToString("yyyy-MM-dd"), pastInstance.PeriodDate);
        Assert.DoesNotContain(instanceList, i => i.PeriodDate == p0.AddDays(7).ToString("yyyy-MM-dd"));
        Assert.DoesNotContain(instanceList, i => i.PeriodDate == p0.AddDays(14).ToString("yyyy-MM-dd"));
        Assert.DoesNotContain(instanceList, i => i.PeriodDate == p0.AddDays(21).ToString("yyyy-MM-dd"));

        // The 3 skipped dates surfaced on the template DTO
        var templateResponse = await client.GetAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}", ct);
        templateResponse.EnsureSuccessStatusCode();
        var templateBody = await templateResponse.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        Assert.Equal(0, templateBody!.Data!.MissedCount);
        Assert.Equal(new[]
        {
            p0.AddDays(7).ToString("yyyy-MM-dd"),
            p0.AddDays(14).ToString("yyyy-MM-dd"),
            p0.AddDays(21).ToString("yyyy-MM-dd"),
        }, templateBody.Data.SkippedPeriodDates);

        // Negative: idempotent — stable counts on a second run
        await RunGenerationJobAsync();
        var (stableExpenses, stableInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, stableExpenses);
        Assert.Equal(1, stableInstances);

        // When the clock reaches the skip boundary, generation resumes forward-only
        AdvanceTime(TimeSpan.FromDays(7));
        Assert.Equal(p0.AddDays(28), JobToday);
        await RunGenerationJobAsync();
        var (forwardExpenses, forwardInstances) = await CountRecurringRowsAsync();
        Assert.Equal(2, forwardExpenses);
        Assert.Equal(2, forwardInstances);
        var forwardList = await GetInstancesAsync(client, group.Id);
        Assert.Contains(forwardList, i => i.PeriodDate == p0.AddDays(28).ToString("yyyy-MM-dd"));
        Assert.Equal(2, forwardList.Count); // still no p0+7/14/21
    }

    [Fact]
    public async Task Job_AdvanceBeyondEndDate_GeneratesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var endDate = p0.AddDays(7);
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0),
            interval: null);

        // Set EndDate via the edit endpoint (create DTO has no endDate)
        var editResponse = await client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}",
            new
            {
                title = "EndDate Test",
                amount = 100m,
                categoryId = 1,
                paymentModeId = 1,
                paidByUserId = admin.Id,
                recurrenceMode = (int)RecurrenceMode.Weekly,
                weekdays = WeekdayBitFor(p0),
                anchorDate = p0.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                splits = new[] { new { userId = admin.Id, splitAmount = 100m } },
            }, ct);
        Assert.True(editResponse.IsSuccessStatusCode, $"edit failed: {editResponse.StatusCode}");

        // Baseline: only p0 (+7d is in the future relative to the fake clock)
        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // Advance to EndDate exactly: inclusive boundary — EndDate occurrence is due
        AdvanceTime(TimeSpan.FromDays(7));
        Assert.Equal(endDate, JobToday);
        await RunGenerationJobAsync();
        var (dueExpenses, dueInstances) = await CountRecurringRowsAsync();
        Assert.Equal(2, dueExpenses);
        Assert.Equal(2, dueInstances);
        var dueInstancesList = await GetInstancesAsync(client, group.Id);
        Assert.Contains(dueInstancesList, i => i.PeriodDate == endDate.ToString("yyyy-MM-dd"));

        // Advance past EndDate: template expired — excluded at query level
        AdvanceTime(TimeSpan.FromDays(1));
        Assert.Equal(endDate.AddDays(1), JobToday);
        await RunGenerationJobAsync();
        var (finalExpenses, finalInstances) = await CountRecurringRowsAsync();
        Assert.Equal(2, finalExpenses);
        Assert.Equal(2, finalInstances);

        var instances = await GetInstancesAsync(client, group.Id);
        Assert.Equal(2, instances.Count);
        Assert.DoesNotContain(instances, i => i.PeriodDate == p0.AddDays(14).ToString("yyyy-MM-dd"));

        // Negative: third run unchanged
        await RunGenerationJobAsync();
        var (thirdExpenses, thirdInstances) = await CountRecurringRowsAsync();
        Assert.Equal(2, thirdExpenses);
        Assert.Equal(2, thirdInstances);
    }

    [Fact]
    public async Task GetTemplate_AfterClockAdvanceToShortMonth_NextOccurrenceIsClamped()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            recurrenceMode: (int)RecurrenceMode.Monthly,
            dayOfMonth: 31,
            anchorDate: "2026-01-31");

        // April 2026 — a 30-day month. Set the shared fake clock directly.
        Factory.TimeProvider.SetUtcNow(new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);

        // Apr 31 clamps to Apr 30; strictly-after-today offset (Apr 11) keeps it the first hit
        Assert.Equal("2026-04-30", body!.Data!.NextOccurrence);
        Assert.Contains("31", body.Data.Summary);
    }

    [Fact]
    public async Task Job_Reconciliation_PastDatedOrphan_CreatesExpenseWithOriginalPeriodDate()
    {
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        // Orphan dated LAST week (relative to the fake clock)
        await SeedOrphanAutoPublishedInstanceAsync(template.Id, p0.AddDays(-7));

        await RunGenerationJobAsync();

        // Final counts: orphan period (p0-7, reconciled) + forward period (p0)
        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(2, expenses);
        Assert.Equal(2, instances);

        // Instance still AutoPublished, ExpenseId set, PeriodDate unchanged
        var instanceList = await GetInstancesAsync(client, group.Id);
        Assert.Equal(2, instanceList.Count);
        var orphanInstance = Assert.Single(instanceList,
            i => i.PeriodDate == p0.AddDays(-7).ToString("yyyy-MM-dd"));
        Assert.Equal((int)RecurringExpenseInstanceStatus.AutoPublished, orphanInstance.Status);
        Assert.NotNull(orphanInstance.ExpenseId);

        // Expense created with the ORIGINAL period date, not today
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templateEntity = await db.RecurringExpenseTemplates
                .FirstAsync(t => t.Guid.ToString() == template.Id);
            var createdExpenses = await db.Expenses
                .Where(e => e.RecurringExpenseTemplateId == templateEntity.Id)
                .ToListAsync();
            Assert.Equal(2, createdExpenses.Count);
            Assert.Contains(createdExpenses, e => e.ExpenseDate == p0.AddDays(-7));
            Assert.Contains(createdExpenses, e => e.ExpenseDate == p0);
            Assert.DoesNotContain(createdExpenses, e => e.ExpenseDate != p0 && e.ExpenseDate != p0.AddDays(-7));
        }
    }

    // --- 10. Resume choice (strategy = backfill | skip) ---

    private async Task<RecurringExpenseTemplateDto> PauseTemplateAsync(
        HttpClient client, string groupId, string templateId)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/recurring-expenses/{templateId}/toggle-active",
            new { isActive = false }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        return body!.Data!;
    }

    private async Task<RecurringExpenseTemplateDto> ResumeTemplateAsync(
        HttpClient client, string groupId, string templateId, string strategy)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/recurring-expenses/{templateId}/resume",
            new { strategy }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        return body!.Data!;
    }

    private async Task<RecurringExpenseTemplateDto> GetTemplateDtoAsync(
        HttpClient client, string groupId, string templateId)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.GetAsync(
            $"/api/v1/groups/{groupId}/recurring-expenses/{templateId}", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        return body!.Data!;
    }

    [Fact]
    public async Task Resume_WithZeroMissed_Reactivates_AndGeneratesNothingExtra()
    {
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // Pause + resume with NO clock advance: nothing missed
        await PauseTemplateAsync(client, group.Id, template.Id);

        var pausedDto = await GetTemplateDtoAsync(client, group.Id, template.Id);
        Assert.Equal(0, pausedDto.MissedCount);

        var resumed = await ResumeTemplateAsync(client, group.Id, template.Id, "skip");
        Assert.True(resumed.IsActive);
        Assert.Null(resumed.PausedReason);

        await RunGenerationJobAsync();
        var (finalExpenses, finalInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, finalExpenses);
        Assert.Equal(1, finalInstances);
    }

    [Fact]
    public async Task Resume_Skip_PastEndDate_ResumesExpired_GeneratesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var endDate = p0.AddDays(7);
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        // Set EndDate via the edit endpoint (create payload has no endDate)
        var editResponse = await client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/recurring-expenses/{template.Id}",
            new
            {
                title = "SkipEndDate Test",
                amount = 100m,
                categoryId = 1,
                paymentModeId = 1,
                paidByUserId = admin.Id,
                recurrenceMode = (int)RecurrenceMode.Weekly,
                weekdays = WeekdayBitFor(p0),
                anchorDate = p0.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                splits = new[] { new { userId = admin.Id, splitAmount = 100m } },
            }, ct);
        Assert.True(editResponse.IsSuccessStatusCode);

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // Pause, then advance PAST EndDate
        await PauseTemplateAsync(client, group.Id, template.Id);
        AdvanceTime(TimeSpan.FromDays(14));
        Assert.Equal(endDate.AddDays(7), JobToday);
        await RunGenerationJobAsync();

        // Missed = p0+7 (EndDate occurrence); ResumeFrom is still null (paused pre-skip)
        var pausedDto = await GetTemplateDtoAsync(client, group.Id, template.Id);
        Assert.Equal(1, pausedDto.MissedCount);

        // Resume skip: boundary = today+1 (no future occurrence — schedule ended)
        var resumed = await ResumeTemplateAsync(client, group.Id, template.Id, "skip");
        Assert.True(resumed.IsActive);
        Assert.Equal(JobToday.AddDays(1).ToString("yyyy-MM-dd"), resumed.ResumeFrom);
        Assert.Equal(new[] { endDate.ToString("yyyy-MM-dd") }, resumed.SkippedPeriodDates);

        // Template is expired: the job excludes it at query level — nothing generated
        await RunGenerationJobAsync();
        var (finalExpenses, finalInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, finalExpenses);
        Assert.Equal(1, finalInstances);
        Assert.Single(await GetInstancesAsync(client, group.Id));
    }

    [Fact]
    public async Task Resume_RequiresApproval_BackfillCreatesPendingOnly_SkipCreatesNeither()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            requiresApproval: true,
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        // Baseline: p0 is a PendingApproval instance, no expense
        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(0, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        // --- Backfill branch ---
        await PauseTemplateAsync(client, group.Id, template.Id);
        AdvanceTime(TimeSpan.FromDays(21));
        await RunGenerationJobAsync();

        var pausedDto = await GetTemplateDtoAsync(client, group.Id, template.Id);
        Assert.Equal(3, pausedDto.MissedCount);

        var backfillResumed = await ResumeTemplateAsync(client, group.Id, template.Id, "backfill");
        Assert.True(backfillResumed.IsActive);
        Assert.Null(backfillResumed.ResumeFrom);

        await RunGenerationJobAsync();
        // 3 missed become PendingApproval instances, ZERO expenses
        var (backfillExpenses, backfillInstances) = await CountRecurringRowsAsync();
        Assert.Equal(0, backfillExpenses);
        Assert.Equal(4, backfillInstances);
        var pendingList = await GetInstancesAsync(client, group.Id, "PendingApproval");
        Assert.Equal(4, pendingList.Count);
        var pendingDates = pendingList.Select(i => i.PeriodDate).OrderBy(d => d).ToList();
        Assert.Equal(new[]
        {
            p0.ToString("yyyy-MM-dd"),
            p0.AddDays(7).ToString("yyyy-MM-dd"),
            p0.AddDays(14).ToString("yyyy-MM-dd"),
            p0.AddDays(21).ToString("yyyy-MM-dd"),
        }, pendingDates);

        // --- Skip branch (separate group: the shared fake clock has already advanced,
        // so a fresh group + fresh template keeps the scenario deterministic) ---
        var groupB = await client.CreateGroupAsync(name: "SkipApproval");
        var skipTemplate = await CreateTemplateAsync(client, groupB.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            requiresApproval: true,
            anchorDate: JobToday.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(JobToday));

        var pSkip = JobToday;
        await RunGenerationJobAsync(); // pSkip pending
        await PauseTemplateAsync(client, groupB.Id, skipTemplate.Id);
        AdvanceTime(TimeSpan.FromDays(21));
        await RunGenerationJobAsync();

        var skippedResumed = await ResumeTemplateAsync(client, groupB.Id, skipTemplate.Id, "skip");
        Assert.True(skippedResumed.IsActive);
        Assert.NotNull(skippedResumed.ResumeFrom);

        await RunGenerationJobAsync();
        // Skip added NOTHING for the MISSED window: the skip template's instances are
        // pSkip (pre-pause) plus forward periods strictly after the skip boundary —
        // the missed pSkip+7/14/21 never appear.
        var allPending = await GetInstancesAsync(client, groupB.Id, "PendingApproval");
        var skipPeriods = allPending
            .Where(i => i.TemplateId == skipTemplate.Id)
            .Select(i => i.PeriodDate).OrderBy(d => d).ToList();
        Assert.DoesNotContain(skipPeriods, d => d == pSkip.AddDays(7).ToString("yyyy-MM-dd"));
        Assert.DoesNotContain(skipPeriods, d => d == pSkip.AddDays(14).ToString("yyyy-MM-dd"));
        Assert.DoesNotContain(skipPeriods, d => d == pSkip.AddDays(21).ToString("yyyy-MM-dd"));
        Assert.Contains(skipPeriods, d => d == pSkip.ToString("yyyy-MM-dd"));
        // Boundary = first occurrence after today (pSkip+28) — still future, so not yet generated
        Assert.DoesNotContain(skipPeriods, d => d == pSkip.AddDays(28).ToString("yyyy-MM-dd"));
        Assert.Equal(new[] { pSkip.ToString("yyyy-MM-dd") }, skipPeriods);
    }

    [Fact]
    public async Task Resume_DoubleResumeAndDoubleJobRun_StaysIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var p0 = JobToday;
        var template = await CreateTemplateAsync(client, group.Id, admin.Id,
            new[] { new { userId = admin.Id, splitAmount = 100m } },
            anchorDate: p0.ToString("yyyy-MM-dd"), weekdays: WeekdayBitFor(p0));

        await RunGenerationJobAsync();
        var (baselineExpenses, baselineInstances) = await CountRecurringRowsAsync();
        Assert.Equal(1, baselineExpenses);
        Assert.Equal(1, baselineInstances);

        await PauseTemplateAsync(client, group.Id, template.Id);
        AdvanceTime(TimeSpan.FromDays(21));

        // Double resume (skip): second call re-computes the boundary from the new today
        var resume1 = await ResumeTemplateAsync(client, group.Id, template.Id, "skip");
        var resume2 = await ResumeTemplateAsync(client, group.Id, template.Id, "skip");
        Assert.True(resume1.IsActive);
        Assert.True(resume2.IsActive);
        // Boundary recomputed from the SAME fake today — identical, not advanced twice
        Assert.Equal(resume1.ResumeFrom, resume2.ResumeFrom);
        Assert.Equal(p0.AddDays(28).ToString("yyyy-MM-dd"), resume2.ResumeFrom);

        // Double job run: stable counts
        await RunGenerationJobAsync();
        await RunGenerationJobAsync();
        var (expenses, instances) = await CountRecurringRowsAsync();
        Assert.Equal(1, expenses);
        Assert.Equal(1, instances);

        // The 3 skipped dates remain surfaced, exactly once each
        var dto = await GetTemplateDtoAsync(client, group.Id, template.Id);
        Assert.Equal(new[]
        {
            p0.AddDays(7).ToString("yyyy-MM-dd"),
            p0.AddDays(14).ToString("yyyy-MM-dd"),
            p0.AddDays(21).ToString("yyyy-MM-dd"),
        }, dto.SkippedPeriodDates);
    }
}