using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.RecurringExpenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Recurring-template setup shared by the push trigger tests.
///
/// The weekday bit is always derived from the template's anchor date, never from
/// "today": the suite runs on a frozen fake clock, so computing the bit from
/// <see cref="DateTime.UtcNow"/> can disagree with the anchor if UTC midnight passes
/// between the clock reset and the payload, turning a weekly template into one with
/// no occurrence today.
/// </summary>
public static class RecurringTestHelpers
{
    /// <summary>Bit position for a weekday, Monday = 1 … Sunday = 64.</summary>
    public static int WeekdayBitFor(DateOnly date) =>
        date.DayOfWeek == DayOfWeek.Sunday ? 1 << 6 : 1 << ((int)date.DayOfWeek - 1);

    public static object TemplatePayload(
        string paidByUserId,
        object splits,
        DateOnly anchor,
        bool requiresApproval = false,
        int recurrenceMode = (int)RecurrenceMode.Weekly)
    {
        return new
        {
            title = "Recurring Test",
            amount = 100m,
            categoryId = 1,
            paymentModeId = 1,
            paidByUserId,
            recurrenceMode,
            weekdays = recurrenceMode == (int)RecurrenceMode.Weekly ? WeekdayBitFor(anchor) : 0,
            dayOfMonth = (int?)null,
            interval = (int?)null,
            anchorDate = anchor.ToString("yyyy-MM-dd"),
            requiresApproval,
            splits,
        };
    }

    public static async Task<RecurringExpenseTemplateDto> CreateTemplateAsync(
        this HttpClient client,
        string groupId,
        string paidByUserId,
        object splits,
        DateOnly anchor,
        bool requiresApproval = false,
        int recurrenceMode = (int)RecurrenceMode.Weekly)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/recurring-expenses",
            TemplatePayload(paidByUserId, splits, anchor, requiresApproval, recurrenceMode), ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<RecurringExpenseTemplateDto>>(ct);
        return body!.Data!;
    }

    /// <summary>Recurring expenses and instances currently persisted (test DB is reset per test).</summary>
    public static async Task<(int expenses, int instances)> CountRecurringRowsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenses = await db.Expenses.CountAsync(
            e => e.RecurringExpenseTemplateId != null, TestContext.Current.CancellationToken);
        var instances = await db.RecurringExpenseInstances.CountAsync(
            TestContext.Current.CancellationToken);
        return (expenses, instances);
    }
}
