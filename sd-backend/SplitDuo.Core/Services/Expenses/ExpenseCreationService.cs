using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.Expenses;

/// <summary>
/// Input for a per-user expense split (decoupled from the Api DTOs so Core
/// and background jobs can create expenses through the same validated path).
/// </summary>
public record ExpenseSplitInput(string UserId, decimal SplitAmount);

/// <summary>
/// Input for an alias-mode expense split (decoupled from the Api DTOs so Core
/// and background jobs can create expenses through the same validated path).
/// </summary>
public record ExpenseAliasSplitInput(string AliasId, decimal SplitAmount);

public interface IExpenseCreationService
{
    Task<Result<Expense>> CreateValidatedExpenseAsync(
        Group group, User paidByUser, DateOnly expenseDate,
        string title, string? description, decimal amount,
        ExpenseCategory category, PaymentMode paymentMode,
        List<ExpenseSplitInput>? splits, List<ExpenseAliasSplitInput>? aliasSplits,
        int? recurringExpenseTemplateId = null);
}

/// <summary>
/// Creates a validated Expense (with split children) without saving — the caller
/// owns SaveChanges/transaction. Validation and localization keys are moved
/// verbatim from ExpensesService.CreateExpenseAsync (behavior-identical).
/// </summary>
public class ExpenseCreationService(
    IUnitOfWork unitOfWork,
    IStringLocalizer<ExpenseCreationService> loc) : IExpenseCreationService
{
    public async Task<Result<Expense>> CreateValidatedExpenseAsync(
        Group group, User paidByUser, DateOnly expenseDate,
        string title, string? description, decimal amount,
        ExpenseCategory category, PaymentMode paymentMode,
        List<ExpenseSplitInput>? splits, List<ExpenseAliasSplitInput>? aliasSplits,
        int? recurringExpenseTemplateId = null)
    {
        // Branch on alias mode vs individual mode
        if (group.UseAliases)
        {
            // Alias-mode branch: validate and create alias splits

            // Reject if alias setup is not finalized
            if (!group.AliasSetupFinalized)
                return Result<Expense>.Conflict(loc["AliasSetupNotFinalized"]);

            // Validate AliasSplits is non-null and non-empty
            if (aliasSplits == null || aliasSplits.Count == 0)
                return Result<Expense>.BadRequest(loc["AliasSplitRequired"]);

            // Check for duplicate AliasId in splits
            var aliasIds = aliasSplits.Select(s => s.AliasId).ToList();
            var duplicateAliases = aliasIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateAliases.Any())
                return Result<Expense>.BadRequest(
                    string.Format(loc["DuplicateAliasesInSplits"], string.Join(", ", duplicateAliases)));

            var totalSplitAmount = 0m;
            var validatedAliases = new Dictionary<Guid, Alias>();

            foreach (var split in aliasSplits)
            {
                if (!Guid.TryParse(split.AliasId, out var aliasGuid))
                    return Result<Expense>.BadRequest(
                        string.Format(loc["InvalidAliasIdInSplit"], split.AliasId));

                var alias = await unitOfWork.Aliases
                    .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

                if (alias == null)
                    return Result<Expense>.BadRequest(
                        string.Format(loc["AliasNotFoundInSplit"], split.AliasId));

                if (alias.GroupId != group.Id)
                    return Result<Expense>.BadRequest(
                        string.Format(loc["AliasNotInGroup"], split.AliasId));

                // Validate split amount
                if (split.SplitAmount <= 0)
                    return Result<Expense>.BadRequest(loc["SplitAmountMustBePositive"]);

                validatedAliases[aliasGuid] = alias;
                totalSplitAmount += split.SplitAmount;
            }

            // Validate that splits sum up to total amount (allow for small rounding differences)
            var difference = Math.Abs(totalSplitAmount - amount);
            if (difference > 0.001m)
                return Result<Expense>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, amount));

            // Look up the payer's current alias to set PaidByAliasId
            var payerMembership = await unitOfWork.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

            // In alias-mode groups, the payer must have an assigned alias before creating
            // expenses. Without an alias, PaidByAliasId would be null and the expense would
            // be silently excluded from balance calculations (issue #31).
            if (payerMembership?.AliasId == null)
            {
                return Result<Expense>.BadRequest(loc["PayerMissingAlias"]);
            }

            // Create expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = title,
                Description = description,
                Amount = amount,
                PaidBy = paidByUser.Id,
                ExpenseDate = expenseDate,
                Category = category,
                PaymentMode = paymentMode,
                PaidByAliasId = payerMembership?.AliasId,
                RecurringExpenseTemplateId = recurringExpenseTemplateId
            };

            // Create alias splits (no ExpenseSplit rows) — reuse cached aliases
            foreach (var split in aliasSplits)
            {
                var aliasGuid = Guid.Parse(split.AliasId);
                var alias = validatedAliases[aliasGuid];

                var expenseAliasSplit = new ExpenseAliasSplit
                {
                    AliasId = alias.Id,
                    Alias = alias,
                    SplitAmount = split.SplitAmount
                };

                expense.ExpenseAliasSplits.Add(expenseAliasSplit);
            }

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for callers (DTO building, background jobs)
            expense.Group = group;
            expense.PaidByUser = paidByUser;

            return Result<Expense>.Success(expense);
        }
        else
        {
            // Individual-mode branch: existing per-user split logic (unchanged)

            // Validate splits
            if (splits == null || splits.Count == 0)
                return Result<Expense>.BadRequest(loc["ExpenseSplitRequired"]);

            // Check for duplicate users in splits
            var userIds = splits.Select(s => s.UserId).ToList();
            var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUsers.Any())
                return Result<Expense>.BadRequest(
                    string.Format(loc["DuplicateUsersInSplits"], string.Join(", ", duplicateUsers)));

            var splitUsers = new List<User>();
            var totalSplitAmount = 0m;

            foreach (var split in splits)
            {
                if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                    return Result<Expense>.BadRequest(
                        string.Format(loc["InvalidUserIdInSplit"], split.UserId));

                var splitUser = await unitOfWork.Users
                    .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

                if (splitUser == null)
                    return Result<Expense>.BadRequest(
                        string.Format(loc["UserNotFoundInSplit"], split.UserId));

                // Check if split user is member of the group
                var isSplitUserMember = await unitOfWork.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

                if (!isSplitUserMember)
                    return Result<Expense>.BadRequest(
                        string.Format(loc["UserNotMemberInSplit"], splitUser.FirstName, splitUser.LastName));

                splitUsers.Add(splitUser);

                // Validate split amount
                if (split.SplitAmount <= 0)
                    return Result<Expense>.BadRequest(loc["SplitAmountMustBePositive"]);

                totalSplitAmount += split.SplitAmount;
            }

            // Validate that splits sum up to total amount (allow for small rounding differences)
            var diff = Math.Abs(totalSplitAmount - amount);
            if (diff > 0.001m)
                return Result<Expense>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, amount));

            // Create expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = title,
                Description = description,
                Amount = amount,
                PaidBy = paidByUser.Id,
                ExpenseDate = expenseDate,
                Category = category,
                PaymentMode = paymentMode,
                RecurringExpenseTemplateId = recurringExpenseTemplateId
            };

            // Create splits
            for (var i = 0; i < splits.Count; i++)
            {
                var split = splits[i];
                var splitUser = splitUsers[i];

                var expenseSplit = new ExpenseSplit
                {
                    UserId = splitUser.Id,
                    User = splitUser,
                    SplitAmount = split.SplitAmount
                };

                expense.ExpenseSplits.Add(expenseSplit);
            }

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for callers (DTO building, background jobs)
            expense.Group = group;
            expense.PaidByUser = paidByUser;

            return Result<Expense>.Success(expense);
        }
    }
}