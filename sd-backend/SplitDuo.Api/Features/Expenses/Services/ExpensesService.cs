using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Services;

public interface IExpensesService
{
    Task<Result<PaginatedResponseDto<ExpenseDto>>> GetGroupExpensesAsync(
        string groupId, Guid currentUserId, int page, int limit, ExpenseFilterOptions filters);

    Task<Result<ExpenseDto>> CreateExpenseAsync(string groupId, Guid currentUserId, CreateExpenseRequestDto request);
    Task<Result<ExpenseDto>> GetExpenseAsync(string groupId, string expenseId, Guid currentUserId);

    Task<Result<ExpenseDto>> UpdateExpenseAsync(string groupId, string expenseId, Guid currentUserId,
        UpdateExpenseRequestDto request);

    Task<Result> DeleteExpenseAsync(string groupId, string expenseId, Guid currentUserId);
}

public class ExpensesService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<ExpensesService> loc) : IExpensesService
{
    public async Task<Result<PaginatedResponseDto<ExpenseDto>>> GetGroupExpensesAsync(
        string groupId, Guid currentUserId, int page, int limit, ExpenseFilterOptions filters)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<ExpenseDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        // Validate page and limit
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<PaginatedResponseDto<ExpenseDto>>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<ExpenseDto>>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<ExpenseDto>>.Forbidden(loc["AccessNotAllowed"]);

        // Build query
        var query = unitOfWork.Expenses
            .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .AsQueryable();

        // Apply filters
        if (DateOnly.TryParse(filters.StartDate, out var startDateOnly))
            query = query.Where(e => e.ExpenseDate >= startDateOnly);

        if (DateOnly.TryParse(filters.EndDate, out var endDateOnly))
            query = query.Where(e => e.ExpenseDate <= endDateOnly);

        if (!string.IsNullOrWhiteSpace(filters.Category) &&
            Enum.TryParse<ExpenseCategory>(filters.Category, true, out var categoryEnum))
            query = query.Where(e => e.CategoryId == (int)categoryEnum);

        if (!string.IsNullOrWhiteSpace(filters.UserId) && Guid.TryParse(filters.UserId, out var userGuid))
        {
            var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Guid == userGuid);
            if (user != null)
                query = query.Where(e => e.PaidBy == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(term) ||
                (e.Description != null && e.Description.ToLower().Contains(term)));
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and ordering
        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        // Load splits for each expense
        var expenseIds = expenses.Select(e => e.Id).ToList();
        var splits = await unitOfWork.ExpenseSplits
            .Where(es => expenseIds.Contains(es.ExpenseId))
            .Include(es => es.User)
            .ToListAsync();

        var splitsByExpense = splits.GroupBy(s => s.ExpenseId).ToDictionary(g => g.Key, g => g.ToList());

        // Load attachment counts for each expense (batch load, avoids N+1)
        var attachmentCounts = await unitOfWork.ExpenseAttachments
            .Where(ea => expenseIds.Contains(ea.ExpenseId))
            .GroupBy(ea => ea.ExpenseId)
            .Select(g => new { ExpenseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ExpenseId, g => g.Count);

        // Load alias splits for alias-mode groups
        List<ExpenseAliasSplit>? aliasSplits = null;
        Dictionary<int, List<ExpenseAliasSplit>>? aliasSplitsByExpense = null;

        if (group.UseAliases)
        {
            aliasSplits = await unitOfWork.ExpenseAliasSplits
                .Where(eas => expenseIds.Contains(eas.ExpenseId))
                .Include(eas => eas.Alias)
                .ToListAsync();

            aliasSplitsByExpense = aliasSplits.GroupBy(s => s.ExpenseId).ToDictionary(g => g.Key, g => g.ToList());
        }

        // Map to DTOs
        var expenseDtos = expenses.Select(expense =>
        {
            var expenseSplits = splitsByExpense.ContainsKey(expense.Id)
                ? splitsByExpense[expense.Id]
                : [];

            List<ExpenseAliasSplit>? expenseAliasSplits = null;
            if (aliasSplitsByExpense != null && aliasSplitsByExpense.ContainsKey(expense.Id))
            {
                expenseAliasSplits = aliasSplitsByExpense[expense.Id];
            }

            var attachmentCount = attachmentCounts.ContainsKey(expense.Id) ? attachmentCounts[expense.Id] : 0;

            return new ExpenseDto(expense, expenseSplits, expenseAliasSplits, attachmentCount);
        }).ToList();

        // Batch avatar lookup for all users referenced by this page of expenses
        var expenseUserIds = expenses.Select(e => e.PaidBy)
            .Concat(splits.Select(s => s.UserId))
            .Distinct()
            .ToList();
        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => expenseUserIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        foreach (var (expense, dto) in expenses.Zip(expenseDtos))
        {
            dto.PaidByUser.HasAvatar = avatarUserIds.Contains(expense.PaidBy);

            var expenseSplits = splitsByExpense.ContainsKey(expense.Id)
                ? splitsByExpense[expense.Id]
                : [];
            for (var i = 0; i < expenseSplits.Count; i++)
            {
                dto.Splits[i].User.HasAvatar = avatarUserIds.Contains(expenseSplits[i].UserId);
            }
        }

        var pagination = new PaginationDto
        {
            Page = page,
            Limit = limit,
            Total = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / limit),
            HasNext = page * limit < totalCount,
            HasPrev = page > 1
        };

        var response = PaginatedResponseDto<ExpenseDto>.SuccessResponse(expenseDtos, pagination);
        return Result<PaginatedResponseDto<ExpenseDto>>.Success(response);
    }

    public async Task<Result<ExpenseDto>> CreateExpenseAsync(string groupId, Guid currentUserId,
        CreateExpenseRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidPaidByUserIdFormat"]);

        if (!DateOnly.TryParse(request.ExpenseDate, out var expenseDate))
            return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseDateFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound(loc["GroupNotFound"]);

        // Check if current user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden(loc["AccessNotAllowed"]);

        // Find the user who paid
        var paidByUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

        if (paidByUser == null)
            return Result<ExpenseDto>.NotFound(loc["PaidByUserNotFound"]);

        // Check if paid by user is member of the group
        var isPaidByUserMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

        if (!isPaidByUserMember)
            return Result<ExpenseDto>.BadRequest(loc["PaidByUserNotMember"]);

        // Parse and validate category
        if (!Enum.TryParse(request.CategoryId.ToString(), true, out ExpenseCategory category) ||
            !Enum.IsDefined(category))
        {
            return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseCategory"]);
        }

        // Parse and validate payment mode
        if (!Enum.TryParse(request.PaymentModeId.ToString(), true, out PaymentMode paymentMode) ||
            !Enum.IsDefined(paymentMode))
        {
            return Result<ExpenseDto>.BadRequest(loc["InvalidExpensePaymentMode"]);
        }

        // Branch on alias mode vs individual mode
        if (group.UseAliases)
        {
            // Alias-mode branch: validate and create alias splits

            // Reject if alias setup is not finalized
            if (!group.AliasSetupFinalized)
                return Result<ExpenseDto>.Conflict(loc["AliasSetupNotFinalized"]);

            // Validate AliasSplits is non-null and non-empty
            if (request.AliasSplits == null || request.AliasSplits.Count == 0)
                return Result<ExpenseDto>.BadRequest(loc["AliasSplitRequired"]);

            // Check for duplicate AliasId in splits
            var aliasIds = request.AliasSplits.Select(s => s.AliasId).ToList();
            var duplicateAliases = aliasIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateAliases.Any())
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["DuplicateAliasesInSplits"], string.Join(", ", duplicateAliases)));

            var totalSplitAmount = 0m;
            var validatedAliases = new Dictionary<Guid, Alias>();

            foreach (var split in request.AliasSplits)
            {
                if (!Guid.TryParse(split.AliasId, out var aliasGuid))
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["InvalidAliasIdInSplit"], split.AliasId));

                var alias = await unitOfWork.Aliases
                    .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

                if (alias == null)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["AliasNotFoundInSplit"], split.AliasId));

                if (alias.GroupId != group.Id)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["AliasNotInGroup"], split.AliasId));

                // Validate split amount
                if (split.SplitAmount <= 0)
                    return Result<ExpenseDto>.BadRequest(loc["SplitAmountMustBePositive"]);

                validatedAliases[aliasGuid] = alias;
                totalSplitAmount += split.SplitAmount;
            }

            // Validate that splits sum up to total amount (allow for small rounding differences)
            var difference = Math.Abs(totalSplitAmount - request.Amount);
            if (difference > 0.001m)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, request.Amount));

            // Look up the payer's current alias to set PaidByAliasId
            var payerMembership = await unitOfWork.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

            // Create expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = request.Title,
                Description = request.Description,
                Amount = request.Amount,
                PaidBy = paidByUser.Id,
                ExpenseDate = expenseDate,
                Category = category,
                PaymentMode = paymentMode,
                PaidByAliasId = payerMembership?.AliasId
            };

            // Create alias splits (no ExpenseSplit rows) — reuse cached aliases
            foreach (var split in request.AliasSplits)
            {
                var aliasGuid = Guid.Parse(split.AliasId);
                var alias = validatedAliases[aliasGuid];

                var expenseAliasSplit = new ExpenseAliasSplit
                {
                    AliasId = alias.Id,
                    SplitAmount = split.SplitAmount
                };

                expense.ExpenseAliasSplits.Add(expenseAliasSplit);
            }

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for the DTO constructor
            expense.Group = group;
            expense.PaidByUser = paidByUser;

            // Map alias splits with Alias nav for DTO — reuse cached aliases
            var aliasSplitsWithAlias = expense.ExpenseAliasSplits.Select(eas =>
            {
                eas.Alias = validatedAliases.Values.First(a => a.Id == eas.AliasId);
                return eas;
            }).ToList();

            var expenseDto = new ExpenseDto(expense, null, aliasSplitsWithAlias);
            await SetHasAvatarAsync(expenseDto, expense, null);
            return Result<ExpenseDto>.Success(expenseDto);
        }
        else
        {
            // Individual-mode branch: existing per-user split logic (unchanged)

            // Validate splits
            if (request.Splits == null || request.Splits.Count == 0)
                return Result<ExpenseDto>.BadRequest(loc["ExpenseSplitRequired"]);

            // Check for duplicate users in splits
            var userIds = request.Splits.Select(s => s.UserId).ToList();
            var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUsers.Any())
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["DuplicateUsersInSplits"], string.Join(", ", duplicateUsers)));

            var splitUsers = new List<User>();
            var totalSplitAmount = 0m;

            foreach (var split in request.Splits)
            {
                if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["InvalidUserIdInSplit"], split.UserId));

                var splitUser = await unitOfWork.Users
                    .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

                if (splitUser == null)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["UserNotFoundInSplit"], split.UserId));

                // Check if split user is member of the group
                var isSplitUserMember = await unitOfWork.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

                if (!isSplitUserMember)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["UserNotMemberInSplit"], splitUser.FirstName, splitUser.LastName));

                splitUsers.Add(splitUser);

                // Validate split amount
                if (split.SplitAmount <= 0)
                    return Result<ExpenseDto>.BadRequest(loc["SplitAmountMustBePositive"]);

                totalSplitAmount += split.SplitAmount;
            }

            // Validate that splits sum up to total amount (allow for small rounding differences)
            var diff = Math.Abs(totalSplitAmount - request.Amount);
            if (diff > 0.001m)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, request.Amount));

            // Create expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = request.Title,
                Description = request.Description,
                Amount = request.Amount,
                PaidBy = paidByUser.Id,
                ExpenseDate = expenseDate,
                Category = category,
                PaymentMode = paymentMode
            };

            // Create splits
            for (var i = 0; i < request.Splits.Count; i++)
            {
                var split = request.Splits[i];
                var splitUser = splitUsers[i];

                var expenseSplit = new ExpenseSplit
                {
                    UserId = splitUser.Id,
                    SplitAmount = split.SplitAmount
                };

                expense.ExpenseSplits.Add(expenseSplit);
            }

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for the DTO constructor
            expense.Group = group;
            expense.PaidByUser = paidByUser;

            // Map splits with users for the DTO
            var splitsWithUsers = expense.ExpenseSplits.Select((split, index) =>
            {
                split.User = splitUsers[index];
                return split;
            }).ToList();

            // Create response DTO
            var expenseDto = new ExpenseDto(expense, splitsWithUsers);
            await SetHasAvatarAsync(expenseDto, expense, splitsWithUsers);
            return Result<ExpenseDto>.Success(expenseDto);
        }
    }

    public async Task<Result<ExpenseDto>> GetExpenseAsync(string groupId, string expenseId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<ExpenseDto>.NotFound(loc["ExpenseNotFound"]);

        // Load splits
        var splits = await unitOfWork.ExpenseSplits
            .Where(es => es.ExpenseId == expense.Id)
            .Include(es => es.User)
            .ToListAsync();

        // Load alias splits if the group is alias-mode
        List<ExpenseAliasSplit>? aliasSplits = null;
        if (group.UseAliases)
        {
            aliasSplits = await unitOfWork.ExpenseAliasSplits
                .Where(eas => eas.ExpenseId == expense.Id)
                .Include(eas => eas.Alias)
                .ToListAsync();
        }

        // Load attachment count
        var attachmentCount = await unitOfWork.ExpenseAttachments
            .CountAsync(ea => ea.ExpenseId == expense.Id);

        var expenseDto = new ExpenseDto(expense, splits, aliasSplits, attachmentCount);
        await SetHasAvatarAsync(expenseDto, expense, splits);

        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result<ExpenseDto>> UpdateExpenseAsync(string groupId, string expenseId, Guid currentUserId,
        UpdateExpenseRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<ExpenseDto>.NotFound(loc["ExpenseNotFound"]);

        // Update basic properties
        if (!string.IsNullOrWhiteSpace(request.Title))
            expense.Title = request.Title;

        if (request.Description != null)
            expense.Description = request.Description;

        if (request.Amount.HasValue)
            expense.Amount = request.Amount.Value;

        if (!string.IsNullOrWhiteSpace(request.ExpenseDate))
        {
            if (!DateOnly.TryParse(request.ExpenseDate, out var expenseDate))
                return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseDateFormat"]);

            expense.ExpenseDate = expenseDate;
        }

        // Parse and validate category (only when provided)
        if (request.CategoryId.HasValue)
        {
            if (!Enum.TryParse(request.CategoryId.Value.ToString(), true, out ExpenseCategory category) ||
                !Enum.IsDefined(category))
            {
                return Result<ExpenseDto>.BadRequest(loc["InvalidExpenseCategory"]);
            }

            expense.Category = category;
        }

        // Parse and validate payment mode (only when provided)
        if (request.PaymentModeId.HasValue)
        {
            if (!Enum.TryParse(request.PaymentModeId.Value.ToString(), true, out PaymentMode paymentMode) ||
                !Enum.IsDefined(paymentMode))
            {
                return Result<ExpenseDto>.BadRequest(loc["InvalidExpensePaymentMode"]);
            }

            expense.PaymentMode = paymentMode;
        }

        if (!string.IsNullOrWhiteSpace(request.PaidByUserId))
        {
            if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
                return Result<ExpenseDto>.BadRequest(loc["InvalidPaidByUserIdFormat"]);

            var paidByUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

            if (paidByUser == null)
                return Result<ExpenseDto>.NotFound(loc["PaidByUserNotFound"]);

            // Check if paid by user is member of the group
            var isPaidByUserMember = await unitOfWork.GroupMembers
                .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

            if (!isPaidByUserMember)
                return Result<ExpenseDto>.BadRequest(loc["PaidByUserNotMember"]);

            expense.PaidBy = paidByUser.Id;
            expense.PaidByUser = paidByUser;

            // Re-derive PaidByAliasId from the new payer's current alias membership
            // (PaidByAliasId is fixed at creation time, but if the payer changes, update it)
            if (group.UseAliases)
            {
                var newPayerMembership = await unitOfWork.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);
                expense.PaidByAliasId = newPayerMembership?.AliasId;
            }
        }

        var newSplits = new List<ExpenseSplit>();
        var newAliasSplits = new List<ExpenseAliasSplit>();

        // Determine if this expense currently has alias splits or user splits
        var hasExistingAliasSplits = await unitOfWork.ExpenseAliasSplits
            .AnyAsync(eas => eas.ExpenseId == expense.Id);

        // Block switching split type (user ↔ alias)
        if (hasExistingAliasSplits && request.Splits is { Count: > 0 })
            return Result<ExpenseDto>.Conflict(loc["CannotChangeSplitTypeToUser"]);

        if (!hasExistingAliasSplits && request.AliasSplits is { Count: > 0 })
            return Result<ExpenseDto>.Conflict(loc["CannotChangeSplitTypeToAlias"]);

        // Update alias splits if provided (alias-mode)
        if (request.AliasSplits is { Count: > 0 })
        {
            // Remove existing alias splits
            var existingAliasSplits = await unitOfWork.ExpenseAliasSplits
                .Where(eas => eas.ExpenseId == expense.Id)
                .ToListAsync();

            unitOfWork.ExpenseAliasSplits.RemoveRange(existingAliasSplits);

            // Validate and create new alias splits
            var aliasIds = request.AliasSplits.Select(s => s.AliasId).ToList();
            var duplicateAliases = aliasIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateAliases.Count > 0)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["DuplicateAliasesInSplits"], string.Join(", ", duplicateAliases)));

            var totalSplitAmount = 0m;

            foreach (var split in request.AliasSplits)
            {
                if (!Guid.TryParse(split.AliasId, out var aliasGuid))
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["InvalidAliasIdInSplit"], split.AliasId));

                var alias = await unitOfWork.Aliases
                    .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

                if (alias == null)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["AliasNotFoundInSplit"], split.AliasId));

                if (alias.GroupId != group.Id)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["AliasNotInGroup"], split.AliasId));

                if (split.SplitAmount <= 0)
                    return Result<ExpenseDto>.BadRequest(loc["SplitAmountMustBePositive"]);

                totalSplitAmount += split.SplitAmount;
            }

            var diff = Math.Abs(totalSplitAmount - expense.Amount);
            if (diff > 0.001m)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, expense.Amount));

            // Create new alias splits
            foreach (var split in request.AliasSplits)
            {
                var aliasGuid = Guid.Parse(split.AliasId);
                var alias = await unitOfWork.Aliases.FirstAsync(a => a.Guid == aliasGuid);

                var expenseAliasSplit = new ExpenseAliasSplit
                {
                    ExpenseId = expense.Id,
                    AliasId = alias.Id,
                    SplitAmount = split.SplitAmount
                };

                unitOfWork.ExpenseAliasSplits.Add(expenseAliasSplit);
                newAliasSplits.Add(expenseAliasSplit);
            }
        }

        // Update user splits if provided (individual-mode)
        if (request.Splits is { Count: > 0 })
        {
            // Remove existing splits
            var existingSplits = await unitOfWork.ExpenseSplits
                .Where(es => es.ExpenseId == expense.Id)
                .ToListAsync();

            unitOfWork.ExpenseSplits.RemoveRange(existingSplits);

            // Validate and create new splits
            var userIds = request.Splits.Select(s => s.UserId).ToList();
            var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUsers.Count > 0)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["DuplicateUsersInSplits"], string.Join(", ", duplicateUsers)));

            var splitUsersByUserId = new Dictionary<int, User>();
            var totalSplitAmount = 0m;

            foreach (var split in request.Splits)
            {
                if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["InvalidUserIdInSplit"], split.UserId));

                var splitUser = await unitOfWork.Users
                    .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

                if (splitUser == null)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["UserNotFoundInSplit"], split.UserId));

                var isSplitUserMember = await unitOfWork.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

                if (!isSplitUserMember)
                    return Result<ExpenseDto>.BadRequest(
                        string.Format(loc["UserNotMemberInSplit"], splitUser.FirstName, splitUser.LastName));

                splitUsersByUserId[splitUser.Id] = splitUser;

                if (split.SplitAmount <= 0)
                    return Result<ExpenseDto>.BadRequest(loc["SplitAmountMustBePositive"]);

                totalSplitAmount += split.SplitAmount;
            }

            var diff = Math.Abs(totalSplitAmount - expense.Amount);
            if (diff > 0.001m)
                return Result<ExpenseDto>.BadRequest(
                    string.Format(loc["SplitAmountsDoNotSum"], totalSplitAmount, expense.Amount));

            foreach (var split in request.Splits)
            {
                var splitUserGuid = Guid.Parse(split.UserId);
                var splitUser = splitUsersByUserId.Values.First(u => u.Guid == splitUserGuid);

                var expenseSplit = new ExpenseSplit
                {
                    ExpenseId = expense.Id,
                    UserId = splitUser.Id,
                    User = splitUser,
                    SplitAmount = split.SplitAmount
                };

                unitOfWork.ExpenseSplits.Add(expenseSplit);
                newSplits.Add(expenseSplit);
            }
        }

        // Load attachment count (attachments are unaffected by updates)
        var attachmentCount = await unitOfWork.ExpenseAttachments
            .CountAsync(ea => ea.ExpenseId == expense.Id);

        // Return with in-memory splits (avoids EF Core identity resolution issue)
        var expenseDto = new ExpenseDto(expense, newSplits, newAliasSplits, attachmentCount);
        await SetHasAvatarAsync(expenseDto, expense, newSplits);
        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result> DeleteExpenseAsync(string groupId, string expenseId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result.BadRequest(loc["InvalidExpenseIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result.NotFound(loc["ExpenseNotFound"]);

        // Soft delete the expense
        expense.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Hard-delete attachments (bytea content must not linger after the expense is gone)
        var attachments = await unitOfWork.ExpenseAttachments
            .Where(a => a.ExpenseId == expense.Id)
            .ToListAsync();

        unitOfWork.ExpenseAttachments.RemoveRange(attachments);

        // Notify other group members
        var otherMembersForDelete = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.UserId != currentUser.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .Where(gm => gm.User.DeletedAt == null)
            .ToListAsync();

        /*foreach (var member in otherMembersForDelete)
        {
            await notificationService.EnqueueAsync(emailTemplateProvider.Render(new ExpenseDeletedModel
            {
                To = member.User.Email, RecipientFirstName = member.User.FirstName,
                DeletedByFirstName = currentUser.FirstName, DeletedByLastName = currentUser.LastName,
                GroupName = group.Name, GroupGuid = group.Guid,
                ExpenseTitle = expense.Title, ExpenseAmount = expense.Amount, ExpenseDate = expense.ExpenseDate
            }));
        }*/

        return Result.Success();
    }

    private async Task SetHasAvatarAsync(ExpenseDto expenseDto, Expense expense, List<ExpenseSplit>? splits)
    {
        var userIds = splits?.Select(s => s.UserId).ToList() ?? [];
        userIds.Add(expense.PaidBy);

        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => userIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        expenseDto.PaidByUser.HasAvatar = avatarUserIds.Contains(expense.PaidBy);

        if (splits != null)
        {
            for (var i = 0; i < splits.Count; i++)
            {
                expenseDto.Splits[i].User.HasAvatar = avatarUserIds.Contains(splits[i].UserId);
            }
        }
    }
}