using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Services.Recurring;

/// <summary>
/// Canonical read shapes for recurring expenses. They live here — not inline in the
/// background job — so tests exercise the exact production shape instead of a copy
/// that silently drifts when the job is edited.
///
/// Both shapes include two collection navigations, so callers must choose the query
/// splitting behavior explicitly. The generation job uses <c>AsSplitQuery()</c> to
/// avoid EF Core event 20504 (multiple-collection include) and the cartesian product
/// a single query would materialize if both collections were ever populated.
/// </summary>
public static class RecurringExpenseQueries
{
    /// <summary>Templates that are due on <paramref name="today"/> and still active.</summary>
    public static IQueryable<RecurringExpenseTemplate> DueTemplates(
        this IQueryable<RecurringExpenseTemplate> query, DateOnly today) =>
        query
            .Include(t => t.Group)
            .Include(t => t.Splits)
            .Include(t => t.AliasSplits)
            .Where(t => t.IsActive
                        && t.DeletedAt == null
                        && t.AnchorDate <= today
                        && (t.EndDate == null || t.EndDate >= today)
                        && t.Group.DeletedAt == null);

    /// <summary>
    /// Auto-published instances that never got their expense (crash between the
    /// instance save and the expense save).
    /// </summary>
    public static IQueryable<RecurringExpenseInstance> OrphanAutoPublishedInstances(
        this IQueryable<RecurringExpenseInstance> query) =>
        query
            .Include(i => i.Template).ThenInclude(t => t.Group)
            .Include(i => i.Template).ThenInclude(t => t.Splits)
            .Include(i => i.Template).ThenInclude(t => t.AliasSplits)
            .Where(i => i.ExpenseId == null
                        && i.StatusId == (int)RecurringExpenseInstanceStatus.AutoPublished
                        && i.DeletedAt == null
                        && i.Template.DeletedAt == null
                        && i.Template.IsActive);
}
