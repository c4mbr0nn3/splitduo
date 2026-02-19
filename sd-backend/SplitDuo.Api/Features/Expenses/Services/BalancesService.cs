using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Services;

public interface IBalancesService
{
    Task<Result<List<BalanceDto>>> GetBalancesAsync(string groupId, Guid currentUserId);
    Task<Result<BalanceSummaryDto>> GetBalanceSummaryAsync(string groupId, Guid currentUserId);
    Task<Result<GroupStatsDto>> GetGroupStatsAsync(string groupId, Guid currentUserId);
}

public class BalancesService(IUnitOfWork unitOfWork) : IBalancesService
{
    public async Task<Result<List<BalanceDto>>> GetBalancesAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<BalanceDto>>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<List<BalanceDto>>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<BalanceDto>>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<BalanceDto>>.Forbidden("Access to this group is not allowed");

        var balances = await CalculateBalancesAsync(group.Id);
        return Result<List<BalanceDto>>.Success(balances);
    }

    public async Task<Result<GroupStatsDto>> GetGroupStatsAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupStatsDto>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<GroupStatsDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupStatsDto>.NotFound("Group not found");

        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<GroupStatsDto>.Forbidden("Access to this group is not allowed");

        var totalExpenses = await unitOfWork.Expenses
            .CountAsync(e => e.GroupId == group.Id && e.DeletedAt == null);

        var totalAmount = await unitOfWork.Expenses
            .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        var balances = await CalculateBalancesAsync(group.Id);

        return Result<GroupStatsDto>.Success(new GroupStatsDto
        {
            TotalExpenses = totalExpenses,
            TotalAmount = totalAmount,
            Balances = balances,
        });
    }

    public async Task<Result<BalanceSummaryDto>> GetBalanceSummaryAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<BalanceSummaryDto>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<BalanceSummaryDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<BalanceSummaryDto>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<BalanceSummaryDto>.Forbidden("Access to this group is not allowed");

        var balances = await CalculateBalancesAsync(group.Id);
        var suggestions = GenerateSettlementSuggestions(balances);

        var summary = new BalanceSummaryDto
        {
            GroupId = groupId,
            Balances = balances,
            Suggestions = suggestions
        };

        return Result<BalanceSummaryDto>.Success(summary);
    }

    private async Task<List<BalanceDto>> CalculateBalancesAsync(int groupId)
    {
        // Get all group members
        var groupMembers = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .Select(gm => gm.User)
            .ToListAsync();

        var balances = new Dictionary<int, BalanceDto>();

        // Initialize balances for all group members
        foreach (var member in groupMembers)
        {
            balances[member.Id] = new BalanceDto
            {
                UserId = member.Guid.ToString(),
                User = new UserBasicInfoDto
                {
                    Id = member.Guid.ToString(),
                    FirstName = member.FirstName,
                    LastName = member.LastName
                },
                Balance = 0,
                TotalPaid = 0,
                TotalOwed = 0
            };
        }

        // Calculate amounts from expenses
        var expenses = await unitOfWork.Expenses
            .Where(e => e.GroupId == groupId && e.DeletedAt == null)
            .Include(e => e.PaidByUser)
            .Include(e => e.ExpenseSplits)
            .ThenInclude(es => es.User)
            .ToListAsync();

        var memberIds = groupMembers.Select(gm => gm.Id).ToHashSet();

        foreach (var expense in expenses)
        {
            // Add amount paid by user
            if (balances.ContainsKey(expense.PaidBy))
            {
                balances[expense.PaidBy].TotalPaid += expense.Amount;
            }

            // Subtract amounts owed by users (from splits) - only for group members
            foreach (var split in expense.ExpenseSplits.Where(es => memberIds.Contains(es.UserId)))
            {
                if (balances.ContainsKey(split.UserId))
                {
                    balances[split.UserId].TotalOwed += split.SplitAmount;
                }
            }
        }

        // Calculate final balances (positive = owed money, negative = owes money)
        foreach (var balance in balances.Values)
        {
            balance.Balance = balance.TotalPaid - balance.TotalOwed;
        }

        return balances.Values.OrderBy(b => b.User.FirstName).ThenBy(b => b.User.LastName).ToList();
    }

    private static List<BalanceSuggestionDto> GenerateSettlementSuggestions(List<BalanceDto> balances)
    {
        var suggestions = new List<BalanceSuggestionDto>();

        // Separate users who owe money (negative balance) from those who are owed money (positive balance)
        var creditors = balances.Where(b => b.Balance > 0.01m).OrderByDescending(b => b.Balance).ToList();
        var debtors = balances.Where(b => b.Balance < -0.01m).OrderBy(b => b.Balance).ToList();

        var creditorQueue = new Queue<(BalanceDto balance, decimal remaining)>(
            creditors.Select(c => (c, c.Balance)));
        var debtorQueue = new Queue<(BalanceDto balance, decimal remaining)>(
            debtors.Select(d => (d, Math.Abs(d.Balance))));

        // Generate optimal settlement suggestions using a greedy algorithm
        while (creditorQueue.Count > 0 && debtorQueue.Count > 0)
        {
            var (creditor, creditorAmount) = creditorQueue.Dequeue();
            var (debtor, debtorAmount) = debtorQueue.Dequeue();

            var settlementAmount = Math.Min(creditorAmount, debtorAmount);

            // Create settlement suggestion
            suggestions.Add(new BalanceSuggestionDto
            {
                FromUserId = debtor.UserId,
                ToUserId = creditor.UserId,
                Amount = Math.Round(settlementAmount, 2),
                Description = $"{debtor.User.FirstName} pays {creditor.User.FirstName}"
            });

            // Update remaining amounts
            var remainingCreditor = creditorAmount - settlementAmount;
            var remainingDebtor = debtorAmount - settlementAmount;

            // Re-queue if there's remaining amount
            if (remainingCreditor > 0.01m)
            {
                creditorQueue.Enqueue((creditor, remainingCreditor));
            }

            if (remainingDebtor > 0.01m)
            {
                debtorQueue.Enqueue((debtor, remainingDebtor));
            }
        }

        return suggestions;
    }
}