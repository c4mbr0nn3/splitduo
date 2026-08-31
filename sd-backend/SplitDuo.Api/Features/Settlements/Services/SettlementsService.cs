using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Settlements.Services;

public class SettlementsService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<SettlementsService> loc) : ISettlementsService
{
    public async Task<Result<PaginatedResponseDto<SettlementDto>>> GetGroupSettlementsAsync(
        string groupId, Guid currentUserId, int page, int limit)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<SettlementDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        // Validate page and limit
        if (page < 1) page = 1;
        if (limit < 1 || limit > 100) limit = 20;

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<PaginatedResponseDto<SettlementDto>>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<SettlementDto>>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<SettlementDto>>.Forbidden(loc["AccessNotAllowed"]);

        // Build query
        var query = unitOfWork.Expenses
            .Where(e => e.GroupId == group.Id && e.DeletedAt == null && e.ExpenseTypeId == (int)ExpenseType.Settlement)
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .Include(e => e.PaidByAlias)
            .Include(e => e.ExpenseSplits).ThenInclude(s => s.User)
            .Include(e => e.ExpenseAliasSplits).ThenInclude(s => s.Alias)
            .AsQueryable();

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Apply pagination and ordering
        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        // Map to DTOs
        var settlementDtos = expenses.Select(e =>
        {
            if (e.ExpenseSplits.Count > 0)
            {
                // Individual mode: one split to the creditor
                var toUser = e.ExpenseSplits.First().User;
                return new SettlementDto(e, e.PaidByUser, toUser);
            }
            else
            {
                // Alias mode
                var fromAlias = e.PaidByAlias;
                var toAlias = e.ExpenseAliasSplits.FirstOrDefault()?.Alias;
                return new SettlementDto(e, e.PaidByUser, null, fromAlias, toAlias);
            }
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

        var response = PaginatedResponseDto<SettlementDto>.SuccessResponse(settlementDtos, pagination);
        return Result<PaginatedResponseDto<SettlementDto>>.Success(response);
    }

    public async Task<Result<SettlementDto>> CreateSettlementAsync(string groupId, Guid currentUserId,
        CreateSettlementRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SettlementDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (request.Amount <= 0)
            return Result<SettlementDto>.BadRequest(loc["SettlementAmountMustBePositive"]);

        if (!DateOnly.TryParse(request.Date, out var settlementDate))
            return Result<SettlementDto>.BadRequest(loc["InvalidSettlementDateFormat"]);

        PaymentMode paymentMode = PaymentMode.Transfer;
        if (request.PaymentModeId.HasValue)
        {
            if (!Enum.IsDefined((PaymentMode)request.PaymentModeId.Value))
                return Result<SettlementDto>.BadRequest(loc["InvalidExpensePaymentMode"]);
            paymentMode = (PaymentMode)request.PaymentModeId.Value;
        }

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SettlementDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SettlementDto>.NotFound(loc["GroupNotFound"]);

        // Check if current user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<SettlementDto>.Forbidden(loc["AccessNotAllowed"]);

        // Resolve the from (debtor) user
        if (!Guid.TryParse(request.FromUserId, out var fromUserGuid))
            return Result<SettlementDto>.BadRequest(loc["FromUserNotFound"]);

        var fromUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == fromUserGuid && u.DeletedAt == null);

        if (fromUser == null)
            return Result<SettlementDto>.BadRequest(loc["FromUserNotFound"]);

        // From user must be a member of the group
        var isFromUserMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == fromUser.Id && gm.DeletedAt == null);

        if (!isFromUserMember)
            return Result<SettlementDto>.BadRequest(loc["FromUserNotMember"]);

        // Branch on alias mode vs individual mode
        if (group.UseAliases)
        {
            // Alias-mode branch

            // Reject if alias setup is not finalized
            if (!group.AliasSetupFinalized)
                return Result<SettlementDto>.BadRequest(loc["AliasSetupNotFinalized"]);

            if (!Guid.TryParse(request.FromAliasId, out var fromAliasGuid))
                return Result<SettlementDto>.BadRequest(loc["FromAliasNotFound"]);

            if (!Guid.TryParse(request.ToAliasId, out var toAliasGuid))
                return Result<SettlementDto>.BadRequest(loc["ToAliasNotFound"]);

            var fromAlias = await unitOfWork.Aliases
                .FirstOrDefaultAsync(a => a.Guid == fromAliasGuid && a.GroupId == group.Id && a.DeletedAt == null);

            if (fromAlias == null)
                return Result<SettlementDto>.BadRequest(loc["FromAliasNotFound"]);

            var toAlias = await unitOfWork.Aliases
                .FirstOrDefaultAsync(a => a.Guid == toAliasGuid && a.GroupId == group.Id && a.DeletedAt == null);

            if (toAlias == null)
                return Result<SettlementDto>.BadRequest(loc["ToAliasNotFound"]);

            // Look up the payer's current alias; it must match the requested fromAlias
            var payerMembership = await unitOfWork.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == fromUser.Id && gm.DeletedAt == null);

            // In alias-mode groups, the payer must have an assigned alias before creating
            // settlements. Without an alias, PaidByAliasId would be null and the expense
            // would be silently excluded from balance calculations.
            if (payerMembership?.AliasId == null || payerMembership.AliasId != fromAlias.Id)
            {
                return Result<SettlementDto>.BadRequest(loc["PayerMissingAlias"]);
            }

            // Create settlement expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = loc["SettlementDefaultTitle"],
                Description = request.Description,
                Amount = request.Amount,
                PaidBy = fromUser.Id,
                ExpenseDate = settlementDate,
                Category = ExpenseCategory.Settlement,
                PaymentMode = paymentMode,
                ExpenseTypeId = (int)ExpenseType.Settlement,
                PaidByAliasId = fromAlias.Id
            };

            // Create alias split (no ExpenseSplit rows)
            var expenseAliasSplit = new ExpenseAliasSplit
            {
                AliasId = toAlias.Id,
                SplitAmount = request.Amount,
                Alias = toAlias
            };

            expense.ExpenseAliasSplits.Add(expenseAliasSplit);

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for the DTO constructor
            expense.Group = group;
            expense.PaidByUser = fromUser;

            var settlementDto = new SettlementDto(expense, fromUser, null, fromAlias, toAlias);
            return Result<SettlementDto>.Success(settlementDto);
        }
        else
        {
            // Individual-mode branch

            if (request.ToUserId == null || !Guid.TryParse(request.ToUserId, out var toUserGuid))
                return Result<SettlementDto>.BadRequest(loc["ToUserNotFound"]);

            var toUser = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Guid == toUserGuid && u.DeletedAt == null);

            if (toUser == null)
                return Result<SettlementDto>.BadRequest(loc["ToUserNotFound"]);

            // To user must be a member of the group
            var isToUserMember = await unitOfWork.GroupMembers
                .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == toUser.Id && gm.DeletedAt == null);

            if (!isToUserMember)
                return Result<SettlementDto>.BadRequest(loc["ToUserNotMember"]);

            // Create settlement expense
            var expense = new Expense
            {
                GroupId = group.Id,
                Title = loc["SettlementDefaultTitle"],
                Description = request.Description,
                Amount = request.Amount,
                PaidBy = fromUser.Id,
                ExpenseDate = settlementDate,
                Category = ExpenseCategory.Settlement,
                PaymentMode = paymentMode,
                ExpenseTypeId = (int)ExpenseType.Settlement
            };

            var split = new ExpenseSplit
            {
                UserId = toUser.Id,
                SplitAmount = request.Amount,
                User = toUser
            };

            expense.ExpenseSplits.Add(split);

            await unitOfWork.Expenses.AddAsync(expense);

            // Set navigation properties for the DTO constructor
            expense.Group = group;
            expense.PaidByUser = fromUser;

            var settlementDto = new SettlementDto(expense, fromUser, toUser);
            return Result<SettlementDto>.Success(settlementDto);
        }
    }

    public async Task<Result<SettlementDto>> GetSettlementAsync(string groupId, string settlementId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<SettlementDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(settlementId, out var settlementGuid))
            return Result<SettlementDto>.BadRequest(loc["InvalidSettlementIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<SettlementDto>.Unauthorized(loc["UserNotAuthenticated"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<SettlementDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<SettlementDto>.Forbidden(loc["AccessNotAllowed"]);

        var settlement = await unitOfWork.Expenses
            .Include(e => e.PaidByUser)
            .Include(e => e.Group)
            .Include(e => e.PaidByAlias)
            .Include(e => e.ExpenseSplits).ThenInclude(s => s.User)
            .Include(e => e.ExpenseAliasSplits).ThenInclude(s => s.Alias)
            .FirstOrDefaultAsync(e => e.Guid == settlementGuid && e.GroupId == group.Id && e.DeletedAt == null &&
                e.ExpenseTypeId == (int)ExpenseType.Settlement);

        if (settlement == null)
            return Result<SettlementDto>.NotFound(loc["SettlementNotFound"]);

        SettlementDto dto;
        if (settlement.ExpenseSplits.Count > 0)
        {
            // Individual mode: one split to the creditor
            var toUser = settlement.ExpenseSplits.First().User;
            dto = new SettlementDto(settlement, settlement.PaidByUser, toUser);
        }
        else
        {
            // Alias mode
            var fromAlias = settlement.PaidByAlias;
            var toAlias = settlement.ExpenseAliasSplits.FirstOrDefault()?.Alias;
            dto = new SettlementDto(settlement, settlement.PaidByUser, null, fromAlias, toAlias);
        }
        return Result<SettlementDto>.Success(dto);
    }

    public async Task<Result> DeleteSettlementAsync(string groupId, string settlementId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(settlementId, out var settlementGuid))
            return Result.BadRequest(loc["InvalidSettlementIdFormat"]);

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

        var settlement = await unitOfWork.Expenses
            .FirstOrDefaultAsync(e => e.Guid == settlementGuid && e.GroupId == group.Id && e.DeletedAt == null && e.ExpenseTypeId == (int)ExpenseType.Settlement);

        if (settlement == null)
            return Result.NotFound(loc["SettlementNotFound"]);

        // Soft delete the settlement
        settlement.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return Result.Success();
    }
}