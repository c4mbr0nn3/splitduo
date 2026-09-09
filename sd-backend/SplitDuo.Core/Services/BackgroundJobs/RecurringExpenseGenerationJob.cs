using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Expenses;
using SplitDuo.Core.Services.Recurring;

namespace SplitDuo.Core.Services.BackgroundJobs;

/// <summary>
/// Generates recurring expense instances for due periods and, for templates that
/// don't require approval, creates the corresponding expense through the shared
/// validated creation path. Runs every 5 minutes; idempotent via the unique
/// (TemplateId, PeriodDate) index.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RecurringExpenseGenerationJob(
    ILogger<RecurringExpenseGenerationJob> logger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ICacheInvalidator cacheInvalidator,
    IExpenseCreationService expenseCreationService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        logger.LogInformation("Starting recurring expense generation for {Date}", today);

        var touchedGroups = new HashSet<Group>();

        // Reconciliation pass: AutoPublished instances that never got their expense
        // (e.g. crash between the instance save and the expense save) are completed first.
        try
        {
            await ReconcileOrphanInstancesAsync(today, touchedGroups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RecurringExpenseGenerationJob: reconciliation pass failed");
        }

        var templates = await unitOfWork.RecurringExpenseTemplates
            .Include(t => t.Group)
            .Include(t => t.Splits)
            .Include(t => t.AliasSplits)
            .Where(t => t.IsActive
                        && t.DeletedAt == null
                        && t.AnchorDate <= today
                        && (t.EndDate == null || t.EndDate >= today)
                        && t.Group.DeletedAt == null)
            .ToListAsync();

        foreach (var template in templates)
        {
            try
            {
                await ProcessTemplateAsync(template, today, touchedGroups);
            }
            catch (Exception ex)
            {
                // One bad template must not kill the batch.
                logger.LogError(ex,
                    "RecurringExpenseGenerationJob: failed processing template {TemplateId}", template.Id);
            }
        }

        foreach (var group in touchedGroups)
        {
            await cacheInvalidator.InvalidateGroupAsync(group.Guid.ToString());
        }

        logger.LogInformation("Recurring expense generation finished for {Date}", today);
    }

    private async Task ProcessTemplateAsync(RecurringExpenseTemplate template, DateOnly today, HashSet<Group> touchedGroups)
    {
        var spec = new RecurrenceEvaluator.RecurrenceSpec(
            template.RecurrenceMode, template.Weekdays, template.DayOfMonth,
            template.Interval, template.AnchorDate, template.EndDate);

        var existingPeriods = await unitOfWork.RecurringExpenseInstances
            .Where(i => i.TemplateId == template.Id && i.DeletedAt == null)
            .Select(i => i.PeriodDate)
            .ToListAsync();

        // Resume-from watermark: after a skip-resume, generation restarts at the
        // first occurrence AFTER the recorded skip boundary. Null ResumeFrom keeps
        // the full [AnchorDate, today] window (backfill semantics).
        var windowStart = template.ResumeFrom.HasValue && template.ResumeFrom.Value > template.AnchorDate
            ? template.ResumeFrom.Value
            : template.AnchorDate;

        var uncovered = RecurrenceEvaluator.GetOccurrences(spec, windowStart, today)
            .Where(p => !existingPeriods.Contains(p))
            .ToList();

        foreach (var period in uncovered)
        {
            try
            {
                await GenerateForPeriodAsync(template, period, touchedGroups);
            }
            catch (DbUpdateException ex)
            {
                // Unique (TemplateId, PeriodDate) violation — concurrent run or re-run.
                // Drop the failed tracked entries so later saves are not poisoned.
                unitOfWork.ClearChangeTracker();
                logger.LogWarning(ex,
                    "RecurringExpenseGenerationJob: instance already exists for template {TemplateId} period {Period}, skipping",
                    template.Id, period);
            }
        }
    }

    private async Task GenerateForPeriodAsync(
        RecurringExpenseTemplate template, DateOnly period, HashSet<Group> touchedGroups)
    {
        var instance = new RecurringExpenseInstance
        {
            TemplateId = template.Id,
            PeriodDate = period,
            Amount = template.Amount,
            Status = template.RequiresApproval
                ? RecurringExpenseInstanceStatus.PendingApproval
                : RecurringExpenseInstanceStatus.AutoPublished
        };

        await unitOfWork.RecurringExpenseInstances.AddAsync(instance);
        // Instance-first ordering: the instance is committed before the expense is
        // attempted, so a crash in between leaves an AutoPublished instance without
        // an expense, which the reconciliation pass completes on the next run.
        await unitOfWork.SaveChangesAsync();

        // Copy the template's split snapshot onto the instance (separate save; the
        // instance row already exists, so these can never violate the unique index).
        foreach (var split in template.Splits)
        {
            await unitOfWork.RecurringExpenseInstanceSplits.AddAsync(new RecurringExpenseInstanceSplit
            {
                InstanceId = instance.Id,
                UserId = split.UserId,
                SplitAmount = split.SplitAmount
            });
        }

        foreach (var aliasSplit in template.AliasSplits)
        {
            await unitOfWork.RecurringExpenseInstanceAliasSplits.AddAsync(new RecurringExpenseInstanceAliasSplit
            {
                InstanceId = instance.Id,
                AliasId = aliasSplit.AliasId,
                SplitAmount = aliasSplit.SplitAmount
            });
        }

        await unitOfWork.SaveChangesAsync();

        touchedGroups.Add(template.Group);

        if (!template.RequiresApproval)
        {
            await CreateExpenseForInstanceAsync(template, instance, touchedGroups);
        }
    }

    private async Task CreateExpenseForInstanceAsync(
        RecurringExpenseTemplate template, RecurringExpenseInstance instance, HashSet<Group> touchedGroups)
    {
        var paidByUser = template.PaidByUser
            ?? await unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == template.PaidByUserId && u.DeletedAt == null);
        if (paidByUser == null)
        {
            await PauseTemplateAsync(template, "Paid-by user no longer exists or was deleted.");
            return;
        }

        List<ExpenseSplitInput>? splits;
        List<ExpenseAliasSplitInput>? aliasSplits;

        if (template.Group.UseAliases)
        {
            var aliasIds = template.AliasSplits.Select(s => s.AliasId).ToList();
            var aliases = await unitOfWork.Aliases
                .Where(a => aliasIds.Contains(a.Id) && a.DeletedAt == null)
                .ToDictionaryAsync(a => a.Id);

            aliasSplits = template.AliasSplits
                .Where(s => aliases.ContainsKey(s.AliasId))
                .Select(s => new ExpenseAliasSplitInput(aliases[s.AliasId].Guid.ToString(), s.SplitAmount))
                .ToList();
            splits = null;
        }
        else
        {
            var userIds = template.Splits.Select(s => s.UserId).ToList();
            var users = await unitOfWork.Users
                .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
                .ToDictionaryAsync(u => u.Id);

            splits = template.Splits
                .Where(s => users.ContainsKey(s.UserId))
                .Select(s => new ExpenseSplitInput(users[s.UserId].Guid.ToString(), s.SplitAmount))
                .ToList();
            aliasSplits = null;
        }

        Result<Expense> result;
        try
        {
            result = await expenseCreationService.CreateValidatedExpenseAsync(
                template.Group, paidByUser, instance.PeriodDate,
                template.Title, template.Description, template.Amount,
                template.Category, template.PaymentMode,
                splits, aliasSplits, template.Id);
        }
        catch (Exception ex)
        {
            await PauseTemplateAsync(template, ex.Message);
            return;
        }

        if (result.IsSuccess)
        {
            // Set the navigation (not ExpenseId) — the expense is still Added/unsaved
            // with Id == 0; the navigation lets EF fix up the FK after SaveChanges
            // generates the store id. Assigning ExpenseId = 0 breaks the save.
            instance.Expense = result.Value;
            instance.Status = RecurringExpenseInstanceStatus.AutoPublished;
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation(
                "RecurringExpenseGenerationJob: created expense {ExpenseId} for template {TemplateId} period {Period}",
                result.Value.Id, template.Id, instance.PeriodDate);
        }
        else
        {
            // Validation failure (member left the group, alias setup broken, ...):
            // pause the template with the error message. The creation service only
            // adds the expense to the repository on the success path, so nothing
            // broken is persisted.
            await PauseTemplateAsync(template, result.Error);
        }
    }

    private async Task PauseTemplateAsync(RecurringExpenseTemplate template, string reason)
    {
        template.IsActive = false;
        template.PausedReason = reason;
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch
        {
            // Drop half-modified tracked entities so subsequent iterations are not poisoned.
            unitOfWork.ClearChangeTracker();
            throw;
        }
        logger.LogWarning(
            "RecurringExpenseGenerationJob: paused template {TemplateId}: {Reason}", template.Id, reason);
    }

    private async Task ReconcileOrphanInstancesAsync(DateOnly today, HashSet<Group> touchedGroups)
    {
        var orphans = await unitOfWork.RecurringExpenseInstances
            .Include(i => i.Template).ThenInclude(t => t.Group)
            .Include(i => i.Template).ThenInclude(t => t.Splits)
            .Include(i => i.Template).ThenInclude(t => t.AliasSplits)
            .Where(i => i.ExpenseId == null
                        && i.StatusId == (int)RecurringExpenseInstanceStatus.AutoPublished
                        && i.DeletedAt == null
                        && i.Template.DeletedAt == null
                        && i.Template.IsActive)
            .ToListAsync();

        foreach (var instance in orphans)
        {
            try
            {
                await CreateExpenseForInstanceAsync(instance.Template, instance, touchedGroups);
            }
            catch (Exception ex)
            {
                unitOfWork.ClearChangeTracker();
                logger.LogError(ex,
                    "RecurringExpenseGenerationJob: reconciliation failed for instance {InstanceId}", instance.Id);
            }
        }
    }
}