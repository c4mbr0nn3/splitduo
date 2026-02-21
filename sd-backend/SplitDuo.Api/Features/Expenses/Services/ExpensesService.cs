using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

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
    INotificationService notificationService,
    IOptions<AppOptions> appOptions) : IExpensesService
{
    private readonly AppOptions _appOptions = appOptions.Value;

    public async Task<Result<PaginatedResponseDto<ExpenseDto>>> GetGroupExpensesAsync(
        string groupId, Guid currentUserId, int page, int limit, ExpenseFilterOptions filters)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<ExpenseDto>>.BadRequest("Invalid group ID format");

        // Validate page and limit
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<PaginatedResponseDto<ExpenseDto>>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<ExpenseDto>>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<ExpenseDto>>.Forbidden("Access to this group is not allowed");

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

        // Map to DTOs
        var expenseDtos = expenses.Select(expense =>
        {
            var expenseSplits = splitsByExpense.ContainsKey(expense.Id)
                ? splitsByExpense[expense.Id]
                : [];

            return new ExpenseDto(expense, expenseSplits);
        }).ToList();

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
            return Result<ExpenseDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
            return Result<ExpenseDto>.BadRequest("Invalid paid by user ID format");

        if (!DateOnly.TryParse(request.ExpenseDate, out var expenseDate))
            return Result<ExpenseDto>.BadRequest("Invalid expense date format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound("Group not found");

        // Check if current user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden("Access to this group is not allowed");

        // Find the user who paid
        var paidByUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

        if (paidByUser == null)
            return Result<ExpenseDto>.NotFound("Paid by user not found");

        // Check if paid by user is member of the group
        var isPaidByUserMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

        if (!isPaidByUserMember)
            return Result<ExpenseDto>.BadRequest("Paid by user is not a member of this group");

        // Parse and validate category
        if (!Enum.TryParse(request.CategoryId.ToString(), true, out ExpenseCategory category))
        {
            return Result<ExpenseDto>.BadRequest("Invalid expense category");
        }

        // Parse and validate payment mode
        if (!Enum.TryParse(request.PaymentModeId.ToString(), true, out PaymentMode paymentMode))
        {
            return Result<ExpenseDto>.BadRequest("Invalid expense payment mode");
        }

        // Validate splits
        if (request.Splits == null || request.Splits.Count == 0)
            return Result<ExpenseDto>.BadRequest("At least one expense split is required");

        // Check for duplicate users in splits
        var userIds = request.Splits.Select(s => s.UserId).ToList();
        var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateUsers.Any())
            return Result<ExpenseDto>.BadRequest(
                $"Duplicate users found in splits: {string.Join(", ", duplicateUsers)}");

        var splitUsers = new List<User>();
        var totalSplitAmount = 0m;

        foreach (var split in request.Splits)
        {
            if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                return Result<ExpenseDto>.BadRequest($"Invalid user ID format in split: {split.UserId}");

            var splitUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

            if (splitUser == null)
                return Result<ExpenseDto>.BadRequest($"User not found in split: {split.UserId}");

            // Check if split user is member of the group
            var isSplitUserMember = await unitOfWork.GroupMembers
                .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

            if (!isSplitUserMember)
                return Result<ExpenseDto>.BadRequest(
                    $"User {splitUser.FirstName} {splitUser.LastName} is not a member of this group");

            splitUsers.Add(splitUser);

            // Validate split amount
            if (split.SplitAmount <= 0)
                return Result<ExpenseDto>.BadRequest("Split amount must be greater than zero");

            totalSplitAmount += split.SplitAmount;
        }

        // Validate that splits sum up to total amount (allow for small rounding differences)
        var difference = Math.Abs(totalSplitAmount - request.Amount);
        if (difference > 0.001m)
            return Result<ExpenseDto>.BadRequest(
                $"Split amounts ({totalSplitAmount:F2}) do not sum up to expense amount ({request.Amount:F2})");

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

        // Notify other group members
        var otherMembersForCreate = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.UserId != currentUser.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .Where(gm => gm.User.DeletedAt == null)
            .ToListAsync();

        foreach (var member in otherMembersForCreate)
        {
            await notificationService.EnqueueAsync(new Notification
            {
                To = member.User.Email,
                Subject = $"New expense in {group.Name}",
                Body = CreateExpenseAddedEmailBody(member.User, currentUser, expense, group)
            });
        }

        // Map splits with users for the DTO
        var splitsWithUsers = expense.ExpenseSplits.Select((split, index) =>
        {
            split.User = splitUsers[index];
            return split;
        }).ToList();

        // Create response DTO
        var expenseDto = new ExpenseDto(expense, splitsWithUsers);

        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result<ExpenseDto>> GetExpenseAsync(string groupId, string expenseId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<ExpenseDto>.BadRequest("Invalid expense ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden("Access to this group is not allowed");

        var expense = await unitOfWork.Expenses
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<ExpenseDto>.NotFound("Expense not found");

        // Load splits
        var splits = await unitOfWork.ExpenseSplits
            .Where(es => es.ExpenseId == expense.Id)
            .Include(es => es.User)
            .ToListAsync();

        var expenseDto = new ExpenseDto(expense, splits);

        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result<ExpenseDto>> UpdateExpenseAsync(string groupId, string expenseId, Guid currentUserId,
        UpdateExpenseRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<ExpenseDto>.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result<ExpenseDto>.BadRequest("Invalid expense ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<ExpenseDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<ExpenseDto>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<ExpenseDto>.Forbidden("Access to this group is not allowed");

        var expense = await unitOfWork.Expenses
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result<ExpenseDto>.NotFound("Expense not found");

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
                return Result<ExpenseDto>.BadRequest("Invalid expense date format");

            expense.ExpenseDate = expenseDate;
        }

        // Parse and validate category
        if (!Enum.TryParse(request.CategoryId.ToString(), true, out ExpenseCategory category))
        {
            return Result<ExpenseDto>.BadRequest("Invalid expense category");
        }

        expense.Category = category;

        // Parse and validate payment mode
        if (!Enum.TryParse(request.PaymentModeId.ToString(), true, out PaymentMode paymentMode))
        {
            return Result<ExpenseDto>.BadRequest("Invalid expense payment mode");
        }

        expense.PaymentMode = paymentMode;

        if (!string.IsNullOrWhiteSpace(request.PaidByUserId))
        {
            if (!Guid.TryParse(request.PaidByUserId, out var paidByUserGuid))
                return Result<ExpenseDto>.BadRequest("Invalid paid by user ID format");

            var paidByUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Guid == paidByUserGuid && u.DeletedAt == null);

            if (paidByUser == null)
                return Result<ExpenseDto>.NotFound("Paid by user not found");

            // Check if paid by user is member of the group
            var isPaidByUserMember = await unitOfWork.GroupMembers
                .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == paidByUser.Id && gm.DeletedAt == null);

            if (!isPaidByUserMember)
                return Result<ExpenseDto>.BadRequest("Paid by user is not a member of this group");

            expense.PaidBy = paidByUser.Id;
            expense.PaidByUser = paidByUser;
        }

        var newSplits = new List<ExpenseSplit>();

        // Update splits if provided
        if (request.Splits is { Count: > 0 })
        {
            // Remove existing splits
            var existingSplits = await unitOfWork.ExpenseSplits
                .Where(es => es.ExpenseId == expense.Id)
                .ToListAsync();

            unitOfWork.ExpenseSplits.RemoveRange(existingSplits);

            // Validate and create new splits
            // Check for duplicate users in splits
            var userIds = request.Splits.Select(s => s.UserId).ToList();
            var duplicateUsers = userIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateUsers.Count > 0)
                return Result<ExpenseDto>.BadRequest(
                    $"Duplicate users found in splits: {string.Join(", ", duplicateUsers)}");

            var splitUsersByUserId = new Dictionary<int, User>();
            var totalSplitAmount = 0m;

            foreach (var split in request.Splits)
            {
                if (!Guid.TryParse(split.UserId, out var splitUserGuid))
                    return Result<ExpenseDto>.BadRequest($"Invalid user ID format in split: {split.UserId}");

                var splitUser = await unitOfWork.Users
                    .FirstOrDefaultAsync(u => u.Guid == splitUserGuid && u.DeletedAt == null);

                if (splitUser == null)
                    return Result<ExpenseDto>.BadRequest($"User not found in split: {split.UserId}");

                // Check if split user is member of the group
                var isSplitUserMember = await unitOfWork.GroupMembers
                    .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == splitUser.Id && gm.DeletedAt == null);

                if (!isSplitUserMember)
                    return Result<ExpenseDto>.BadRequest(
                        $"User {splitUser.FirstName} {splitUser.LastName} is not a member of this group");

                splitUsersByUserId[splitUser.Id] = splitUser;

                // Validate split amount
                if (split.SplitAmount <= 0)
                    return Result<ExpenseDto>.BadRequest("Split amount must be greater than zero");

                totalSplitAmount += split.SplitAmount;
            }

            // Validate that splits sum up to total amount (allow for small rounding differences)
            var difference = Math.Abs(totalSplitAmount - expense.Amount);
            if (difference > 0.001m)
                return Result<ExpenseDto>.BadRequest(
                    $"Split amounts ({totalSplitAmount:F2}) do not sum up to expense amount ({expense.Amount:F2})");

            // Create new splits
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

        // Return with in-memory splits (avoids EF Core identity resolution issue)
        var expenseDto = new ExpenseDto(expense, newSplits);
        return Result<ExpenseDto>.Success(expenseDto);
    }

    public async Task<Result> DeleteExpenseAsync(string groupId, string expenseId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(expenseId, out var expenseGuid))
            return Result.BadRequest("Invalid expense ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result.Forbidden("Access to this group is not allowed");

        var expense = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == expenseGuid && e.GroupId == group.Id && e.DeletedAt == null);

        if (expense == null)
            return Result.NotFound("Expense not found");

        // Soft delete the expense
        expense.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Notify other group members
        var otherMembersForDelete = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.UserId != currentUser.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .Where(gm => gm.User.DeletedAt == null)
            .ToListAsync();

        foreach (var member in otherMembersForDelete)
        {
            await notificationService.EnqueueAsync(new Notification
            {
                To = member.User.Email,
                Subject = $"Expense removed from {group.Name}",
                Body = CreateExpenseDeletedEmailBody(member.User, currentUser, expense, group)
            });
        }

        return Result.Success();
    }

    private string CreateExpenseAddedEmailBody(User recipient, User addedBy, Expense expense, Group group)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{group.Guid}";
        return $"""
                <p>Hello {recipient.FirstName},</p>
                <p>{addedBy.FirstName} {addedBy.LastName} added a new expense to <strong>{group.Name}</strong>:</p>
                <p><strong>{expense.Title}</strong> &mdash; {expense.Amount:F2} on {expense.ExpenseDate:yyyy-MM-dd}</p>
                <p><a href="{groupUrl}">View group</a></p>
                <p>Best regards,<br>The SplitDuo Team</p>
                """;
    }

    private string CreateExpenseDeletedEmailBody(User recipient, User deletedBy, Expense expense, Group group)
    {
        var groupUrl = $"{_appOptions.BaseUrl}/groups/{group.Guid}";
        return $"""
                <p>Hello {recipient.FirstName},</p>
                <p>{deletedBy.FirstName} {deletedBy.LastName} removed an expense from <strong>{group.Name}</strong>:</p>
                <p><strong>{expense.Title}</strong> &mdash; {expense.Amount:F2} on {expense.ExpenseDate:yyyy-MM-dd}</p>
                <p><a href="{groupUrl}">View group</a></p>
                <p>Best regards,<br>The SplitDuo Team</p>
                """;
    }
}