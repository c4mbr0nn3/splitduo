using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Services;

public interface IBalancesService
{
    Task<Result<List<BalanceDto>>> GetBalancesAsync(string groupId, Guid currentUserId);
    Task<Result<List<AliasBalanceDto>>> GetAliasBalancesAsync(string groupId, Guid currentUserId);
    Task<Result<BalanceSummaryDto>> GetBalanceSummaryAsync(string groupId, Guid currentUserId);
    Task<Result<AliasBalanceSummaryDto>> GetAliasBalanceSummaryAsync(string groupId, Guid currentUserId);
    Task<Result<GroupStatsDto>> GetGroupStatsAsync(string groupId, Guid currentUserId);
    Task<Result<UserStatsDto>> GetCurrentUserStatsAsync(Guid currentUserId);
}

public class BalancesService(
    IUnitOfWork unitOfWork,
    HybridCache cache,
    IStringLocalizer<BalancesService> loc) : IBalancesService
{
    public async Task<Result<UserStatsDto>> GetCurrentUserStatsAsync(Guid currentUserId)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null) return Result<UserStatsDto>.NotFound(loc["UserNotFound"]);

        // Fetch the user's group memberships with mode info (individual vs alias).
        // Alias-mode groups never create ExpenseSplit rows — they use ExpenseAliasSplit
        // keyed by AliasId, so balances must be computed at the alias level.
        var userMemberships = await unitOfWork.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == user.Id && gm.DeletedAt == null)
            .Join(unitOfWork.Groups.AsNoTracking().Where(g => g.DeletedAt == null),
                gm => gm.GroupId, g => g.Id,
                (gm, g) => new { gm.GroupId, g.UseAliases, gm.AliasId })
            .ToListAsync();

        var userGroupIds = userMemberships.Select(m => m.GroupId).ToList();
        var individualGroupIds = userMemberships.Where(m => !m.UseAliases).Select(m => m.GroupId).ToList();
        var aliasGroupIds = userMemberships.Where(m => m.UseAliases).Select(m => m.GroupId).ToList();

        // Per-group: how much the user paid (INDIVIDUAL MODE ONLY)
        var paidByGroup = individualGroupIds.Count > 0
            ? await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => individualGroupIds.Contains(e.GroupId) && e.PaidBy == user.Id && e.DeletedAt == null)
                .GroupBy(e => e.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total)
            : new Dictionary<int, decimal>();

        // Per-group: how much of the user's split share (INDIVIDUAL MODE ONLY)
        var splitByGroup = individualGroupIds.Count > 0
            ? await unitOfWork.ExpenseSplits
                .AsNoTracking()
                .Where(es => es.UserId == user.Id)
                .Join(unitOfWork.Expenses.AsNoTracking().Where(e => e.DeletedAt == null),
                    es => es.ExpenseId, e => e.Id,
                    (es, e) => new { e.GroupId, es.SplitAmount })
                .Where(x => individualGroupIds.Contains(x.GroupId))
                .GroupBy(x => x.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Sum(x => x.SplitAmount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total)
            : new Dictionary<int, decimal>();

        // Alias-mode "paid" per (group, alias): sum of expenses paid by the alias, with
        // COALESCE(PaidByAliasId, payer's current alias) fallback — same semantics as
        // GroupsService.GetUserGroupsAsync and BalancesService.CalculateAliasBalancesAsync.
        // LEFT JOIN group_members so expenses with null PaidByAliasId (pre-migration data)
        // are attributed to the payer's current alias.
        var aliasPaidByGroup = aliasGroupIds.Count > 0
            ? await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null)
                .GroupJoin(
                    unitOfWork.GroupMembers.AsNoTracking()
                        .Where(gm => gm.DeletedAt == null && gm.AliasId != null),
                    e => new { e.GroupId, e.PaidBy },
                    gm => new { GroupId = gm.GroupId, PaidBy = gm.UserId },
                    (e, gms) => new { e, gms })
                .SelectMany(
                    x => x.gms.DefaultIfEmpty(),
                    (x, gm) => new { x.e.GroupId, AliasId = x.e.PaidByAliasId ?? (gm == null ? null : gm.AliasId), x.e.Amount })
                .Where(x => x.AliasId != null)
                .GroupBy(x => new { x.GroupId, x.AliasId })
                .Select(g => new { g.Key.GroupId, g.Key.AliasId, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => (x.GroupId, x.AliasId!.Value), x => x.Total)
            : new Dictionary<(int GroupId, int AliasId), decimal>();

        // Alias-mode "owed" per (group, alias): sum of ExpenseAliasSplit.SplitAmount
        // over non-deleted expenses (same pattern as GroupsService.GetUserGroupsAsync)
        var aliasSplitByGroup = aliasGroupIds.Count > 0
            ? await unitOfWork.ExpenseAliasSplits
                .AsNoTracking()
                .Join(unitOfWork.Expenses.AsNoTracking()
                        .Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null),
                    eas => eas.ExpenseId, e => e.Id,
                    (eas, e) => new { e.GroupId, eas.AliasId, eas.SplitAmount })
                .GroupBy(x => new { x.GroupId, x.AliasId })
                .Select(g => new { g.Key.GroupId, g.Key.AliasId, Total = g.Sum(x => x.SplitAmount) })
                .ToDictionaryAsync(x => (x.GroupId, x.AliasId), x => x.Total)
            : new Dictionary<(int GroupId, int AliasId), decimal>();

        // Sum per-group nets per mode: positive → user is owed, negative → user owes
        var individualYouOwe = 0m;
        var individualYoureOwed = 0m;
        var aliasYouOwe = 0m;
        var aliasYoureOwed = 0m;
        foreach (var membership in userMemberships)
        {
            decimal net;
            if (membership.UseAliases)
            {
                // Alias-mode: the user's net is their alias's net (paid − owed)
                if (membership.AliasId == null)
                {
                    net = 0m;
                }
                else
                {
                    var aliasId = membership.AliasId.Value;
                    net = aliasPaidByGroup.GetValueOrDefault((membership.GroupId, aliasId), 0m)
                          - aliasSplitByGroup.GetValueOrDefault((membership.GroupId, aliasId), 0m);
                }
            }
            else
            {
                // Individual-mode: existing per-user computation
                net = paidByGroup.GetValueOrDefault(membership.GroupId, 0m)
                      - splitByGroup.GetValueOrDefault(membership.GroupId, 0m);
            }

            if (membership.UseAliases)
            {
                if (net > 0) aliasYoureOwed += net;
                else aliasYouOwe += -net;
            }
            else
            {
                if (net > 0) individualYoureOwed += net;
                else individualYouOwe += -net;
            }
        }

        var stats = new UserStatsDto
        {
            TotalGroups = individualGroupIds.Count + aliasGroupIds.Count,
            Individual = new ModeBalanceDto
            {
                Groups = individualGroupIds.Count,
                YouOwe = individualYouOwe,
                YoureOwed = individualYoureOwed
            },
            Alias = new ModeBalanceDto
            {
                Groups = aliasGroupIds.Count,
                YouOwe = aliasYouOwe,
                YoureOwed = aliasYoureOwed
            }
        };

        return Result<UserStatsDto>.Success(stats);
    }

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

        var groupGuidStr = groupGuid.ToString();
        var balances = await cache.GetOrCreateAsync(
            $"balances:group:{groupGuidStr}",
            async ct => await CalculateBalancesAsync(group.Id),
            tags: [$"group:{groupGuidStr}"]);
        return Result<List<BalanceDto>>.Success(balances);
    }

    public async Task<Result<List<AliasBalanceDto>>> GetAliasBalancesAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<AliasBalanceDto>>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<List<AliasBalanceDto>>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<AliasBalanceDto>>.NotFound("Group not found");

        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<AliasBalanceDto>>.Forbidden("Access to this group is not allowed");

        var groupGuidStr = groupGuid.ToString();
        var balances = await cache.GetOrCreateAsync(
            $"aliasbalances:group:{groupGuidStr}",
            async ct => await CalculateAliasBalancesAsync(group.Id),
            tags: [$"group:{groupGuidStr}"]);
        return Result<List<AliasBalanceDto>>.Success(balances);
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

        var groupGuidStr = groupGuid.ToString();
        var stats = await cache.GetOrCreateAsync(
            $"groupstats:group:{groupGuidStr}",
            async ct =>
            {
                // ExpenseCount = number of expense records; TotalAmount = summed currency
                var expenseCount = await unitOfWork.Expenses
                    .CountAsync(e => e.GroupId == group.Id && e.DeletedAt == null, ct);

                var totalAmount = await unitOfWork.Expenses
                    .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
                    .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

                List<BalanceDto> balances;
                if (group.UseAliases)
                {
                    var aliasBalances = await CalculateAliasBalancesAsync(group.Id);
                    // For GroupStatsDto, we still need BalanceDto list — convert alias balances to a flat
                    // per-user representation for backward compatibility, or leave empty.
                    // The GroupStatsDto is used by the frontend stats view; for alias-mode groups,
                    // the per-user balance is not meaningful. Return empty list.
                    balances = [];
                }
                else
                {
                    balances = await CalculateBalancesAsync(group.Id);
                }

                var categoryData = await unitOfWork.Expenses
                    .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
                    .GroupBy(e => e.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Amount = g.Sum(e => (decimal?)e.Amount) ?? 0m, Count = g.Count() })
                    .OrderByDescending(x => x.Amount)
                    .ToListAsync(ct);

                var categoryBreakdown = categoryData
                    .Select(x => new CategoryStatDto
                    {
                        CategoryId = x.CategoryId,
                        CategoryName = ((ExpenseCategory)x.CategoryId).ToString(),
                        Amount = x.Amount,
                        Count = x.Count
                    }).ToList();

                var monthlyData = await unitOfWork.Expenses
                    .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
                    .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                    .Select(g => new
                    {
                        g.Key.Year, g.Key.Month, Amount = g.Sum(e => (decimal?)e.Amount) ?? 0m, Count = g.Count()
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToListAsync(ct);

                var monthlyBreakdown = monthlyData
                    .Select(x => new MonthlyStatDto { Year = x.Year, Month = x.Month, Amount = x.Amount, Count = x.Count })
                    .ToList();

                return new GroupStatsDto
                {
                    ExpenseCount = expenseCount,
                    TotalAmount = totalAmount,
                    Balances = balances,
                    CategoryBreakdown = categoryBreakdown,
                    MonthlyBreakdown = monthlyBreakdown,
                };
            },
            tags: [$"group:{groupGuidStr}"]);

        return Result<GroupStatsDto>.Success(stats);
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

        var groupGuidStr = groupGuid.ToString();
        var summary = await cache.GetOrCreateAsync(
            $"balancesummary:group:{groupGuidStr}",
            async ct =>
            {
                var balances = await CalculateBalancesAsync(group.Id);
                var suggestions = GenerateSettlementSuggestions(balances);
                return new BalanceSummaryDto
                {
                    GroupId = groupId,
                    Balances = balances,
                    Suggestions = suggestions
                };
            },
            tags: [$"group:{groupGuidStr}"]);

        return Result<BalanceSummaryDto>.Success(summary);
    }

    public async Task<Result<AliasBalanceSummaryDto>> GetAliasBalanceSummaryAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<AliasBalanceSummaryDto>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<AliasBalanceSummaryDto>.Unauthorized("User not authenticated");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<AliasBalanceSummaryDto>.NotFound("Group not found");

        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<AliasBalanceSummaryDto>.Forbidden("Access to this group is not allowed");

        var groupGuidStr = groupGuid.ToString();
        var summary = await cache.GetOrCreateAsync(
            $"aliassummary:group:{groupGuidStr}",
            async ct =>
            {
                var balances = await CalculateAliasBalancesAsync(group.Id);
                var suggestions = GenerateAliasSettlementSuggestions(balances);
                return new AliasBalanceSummaryDto
                {
                    GroupId = groupId,
                    Balances = balances,
                    Suggestions = suggestions
                };
            },
            tags: [$"group:{groupGuidStr}"]);

        return Result<AliasBalanceSummaryDto>.Success(summary);
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

        var balanceMemberIds = groupMembers.Select(m => m.Id).ToList();
        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => balanceMemberIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

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
                    LastName = member.LastName,
                    HasAvatar = avatarUserIds.Contains(member.Id)
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

    /// <summary>
    /// Calculates alias-level balances for alias-mode groups.
    ///
    /// TotalPaid per alias = sum of Expense.Amount over non-deleted expenses where
    /// Expense.PaidByAliasId == alias.Id. This captures the payer's alias at the time
    /// of payment, making paid/owed symmetric and historically accurate.
    ///
    /// For expenses with null PaidByAliasId (backward compat with pre-migration data),
    /// falls back to current-membership attribution: looks up the payer's current
    /// GroupMember.AliasId. This preserves balance correctness for any expenses created
    /// before the PaidByAliasId migration.
    ///
    /// TotalOwed per alias = sum of ExpenseAliasSplit.SplitAmount for that alias
    /// (over non-deleted expenses). This naturally includes soft-deleted aliases
    /// referenced by historical splits.
    /// </summary>
    private async Task<List<AliasBalanceDto>> CalculateAliasBalancesAsync(int groupId)
    {
        // Load all non-deleted aliases in the group (include members → users)
        var aliases = await unitOfWork.Aliases
            .Where(a => a.GroupId == groupId && a.DeletedAt == null)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .ToListAsync();

        // Also load soft-deleted aliases that are referenced by non-deleted ExpenseAliasSplit rows
        // (for balance integrity — EXT-003 / edge case "balance with soft-deleted alias")
        var softDeletedAliasIds = await unitOfWork.ExpenseAliasSplits
            .Where(eas => eas.Expense.DeletedAt == null)
            .Select(eas => eas.AliasId)
            .Distinct()
            .ToListAsync();

        var softDeletedAliasesWithSplits = await unitOfWork.Aliases
            .Where(a => a.GroupId == groupId && a.DeletedAt != null && softDeletedAliasIds.Contains(a.Id))
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .ToListAsync();

        var allAliases = aliases.Concat(softDeletedAliasesWithSplits).ToList();

        // Build a dictionary keyed by alias Id
        var aliasBalances = new Dictionary<int, AliasBalanceDto>();

        var aliasMemberIds = allAliases
            .SelectMany(a => a.Members.Where(m => m.DeletedAt == null))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var aliasAvatarUserIds = await unitOfWork.UserAvatars
            .Where(a => aliasMemberIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        foreach (var alias in allAliases)
        {
            var activeMembers = alias.Members.Where(m => m.DeletedAt == null).ToList();

            aliasBalances[alias.Id] = new AliasBalanceDto
            {
                AliasId = alias.Guid.ToString(),
                AliasName = alias.Name,
                Balance = 0,
                TotalPaid = 0,
                TotalOwed = 0,
                IsSingleton = alias.IsSingleton ?? false,
                Members = activeMembers.Select(m => new UserBasicInfoDto
                {
                    Id = m.User.Guid.ToString(),
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    HasAvatar = aliasAvatarUserIds.Contains(m.UserId)
                }).ToList()
            };
        }

        // Get all non-deleted expenses for the group
        var expenses = await unitOfWork.Expenses
            .Where(e => e.GroupId == groupId && e.DeletedAt == null)
            .ToListAsync();

        // Get current alias membership for fallback (expenses with null PaidByAliasId)
        var currentMemberships = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.DeletedAt == null && gm.AliasId != null)
            .Select(gm => new { gm.UserId, gm.AliasId })
            .ToListAsync();

        var userAliasMap = currentMemberships
            .Where(x => x.AliasId != null)
            .ToDictionary(x => x.UserId, x => x.AliasId!.Value);

        // TotalPaid per alias: use PaidByAliasId (historical), fall back to current membership
        foreach (var expense in expenses)
        {
            int? payerAliasId;

            if (expense.PaidByAliasId != null)
            {
                // Historical attribution — use the alias at payment time
                payerAliasId = expense.PaidByAliasId;
            }
            else if (userAliasMap.TryGetValue(expense.PaidBy, out var currentAliasId))
            {
                // Fallback for pre-migration data — use current membership
                payerAliasId = currentAliasId;
            }
            else
            {
                continue;
            }

            if (payerAliasId.HasValue && aliasBalances.ContainsKey(payerAliasId.Value))
            {
                aliasBalances[payerAliasId.Value].TotalPaid += expense.Amount;
            }
        }

        // TotalOwed per alias: sum of ExpenseAliasSplit.SplitAmount for that alias
        // (over non-deleted expenses). This naturally includes soft-deleted aliases
        // referenced by historical splits.
        var expenseIds = expenses.Select(e => e.Id).ToHashSet();

        var aliasSplits = await unitOfWork.ExpenseAliasSplits
            .Where(eas => expenseIds.Contains(eas.ExpenseId))
            .ToListAsync();

        foreach (var split in aliasSplits)
        {
            if (aliasBalances.ContainsKey(split.AliasId))
            {
                aliasBalances[split.AliasId].TotalOwed += split.SplitAmount;
            }
        }

        // Calculate final balances
        foreach (var balance in aliasBalances.Values)
        {
            balance.Balance = balance.TotalPaid - balance.TotalOwed;
        }

        return aliasBalances.Values.OrderBy(b => b.AliasName).ToList();
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

    /// <summary>
    /// Generates alias-level settlement suggestions using the greedy algorithm.
    /// </summary>
    private static List<AliasSettlementSuggestionDto> GenerateAliasSettlementSuggestions(
        List<AliasBalanceDto> balances)
    {
        var suggestions = new List<AliasSettlementSuggestionDto>();

        var creditors = balances.Where(b => b.Balance > 0.01m).OrderByDescending(b => b.Balance).ToList();
        var debtors = balances.Where(b => b.Balance < -0.01m).OrderBy(b => b.Balance).ToList();

        var creditorQueue = new Queue<(AliasBalanceDto balance, decimal remaining)>(
            creditors.Select(c => (c, c.Balance)));
        var debtorQueue = new Queue<(AliasBalanceDto balance, decimal remaining)>(
            debtors.Select(d => (d, Math.Abs(d.Balance))));

        while (creditorQueue.Count > 0 && debtorQueue.Count > 0)
        {
            var (creditor, creditorAmount) = creditorQueue.Dequeue();
            var (debtor, debtorAmount) = debtorQueue.Dequeue();

            var settlementAmount = Math.Min(creditorAmount, debtorAmount);

            suggestions.Add(new AliasSettlementSuggestionDto
            {
                FromAliasId = debtor.AliasId,
                ToAliasId = creditor.AliasId,
                FromAliasName = debtor.AliasName,
                ToAliasName = creditor.AliasName,
                Amount = Math.Round(settlementAmount, 2),
                Description = $"{debtor.AliasName} owes {creditor.AliasName} {settlementAmount:F2}"
            });

            var remainingCreditor = creditorAmount - settlementAmount;
            var remainingDebtor = debtorAmount - settlementAmount;

            if (remainingCreditor > 0.01m)
                creditorQueue.Enqueue((creditor, remainingCreditor));

            if (remainingDebtor > 0.01m)
                debtorQueue.Enqueue((debtor, remainingDebtor));
        }

        return suggestions;
    }
}
