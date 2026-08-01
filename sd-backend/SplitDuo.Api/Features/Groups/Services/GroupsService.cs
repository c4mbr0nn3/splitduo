using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Aliases.Services;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Email;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Groups.Services;

public interface IGroupsService
{
    Task<Result<List<GroupDto>>> GetUserGroupsAsync(Guid currentUserId, int? limit = null);
    Task<Result<GroupDto>> CreateGroupAsync(Guid currentUserId, CreateGroupRequestDto request);
    Task<Result<GroupDto>> GetGroupAsync(string groupId, Guid currentUserId);
    Task<Result<GroupDto>> UpdateGroupAsync(string groupId, Guid currentUserId, UpdateGroupRequestDto request);
    Task<Result> DeleteGroupAsync(string groupId, Guid currentUserId);
    Task<Result<List<GroupMemberDto>>> GetGroupMembersAsync(string groupId, Guid currentUserId);

    Task<Result<GroupMemberDto>> AddGroupMemberAsync(string groupId, Guid currentUserId,
        AddGroupMemberRequestDto request);

    Task<Result> RemoveGroupMemberAsync(string groupId, string userId, Guid currentUserId);

    Task<Result<GroupMemberDto>> ChangeMemberRoleAsync(string groupId, string userId, Guid currentUserId,
        UpdateGroupMemberRoleRequestDto request);

    Task<Result<PaginatedResponseDto<ImportStatusDto>>> GetGroupImportsAsync(
        string groupId,
        Guid currentUserId,
        int page = 1,
        int limit = 20);

    Task<Result<ImportStatusDto>> GetImportStatusAsync(Guid importId, Guid currentUserId);
}

public class GroupsService(
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    IEmailTemplateProvider emailTemplateProvider,
    TimeProvider timeProvider,
    IStringLocalizer<GroupsService> loc) : IGroupsService
{
    public async Task<Result<List<GroupDto>>> GetUserGroupsAsync(Guid currentUserId, int? limit = null)
    {
        var user = await unitOfWork.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<GroupDto>>.Unauthorized(loc["UserNotFound"]);

        var allGroups = await unitOfWork.GroupMembers
            .AsNoTracking()
            .Where(gm => gm.UserId == user.Id && gm.DeletedAt == null)
            .Include(gm => gm.Group)
            .Where(gm => gm.Group.DeletedAt == null)
            .Select(gm => new
            {
                Group = gm.Group,
                MemberCount = unitOfWork.GroupMembers.Count(m => m.GroupId == gm.Group.Id && m.DeletedAt == null)
            })
            .ToListAsync();

        var allGroupIds = allGroups.Select(ug => ug.Group.Id).ToList();

        var lastActivityByGroup = await unitOfWork.Expenses
            .AsNoTracking()
            .Where(e => allGroupIds.Contains(e.GroupId) && e.DeletedAt == null)
            .GroupBy(e => e.GroupId)
            .Select(g => new { GroupId = g.Key, LastActivity = g.Max(e => e.CreatedAt) })
            .ToDictionaryAsync(x => x.GroupId, x => x.LastActivity);

        var userGroups = allGroups
            .OrderByDescending(ug => lastActivityByGroup.GetValueOrDefault(ug.Group.Id, 0L))
            .Take(limit ?? allGroups.Count)
            .ToList();

        var groupIds = userGroups.Select(ug => ug.Group.Id).ToList();

        // Split groups into individual-mode and alias-mode for balance computation.
        // For individual-mode groups: use the existing per-user ExpenseSplit aggregation.
        // For alias-mode groups: find the user's alias, then compute the alias's net balance
        // (TotalPaid = sum of expenses paid by any member of the alias; TotalOwed = sum of
        // ExpenseAliasSplit.SplitAmount for that alias). The user's NetBalance is their alias's
        // net balance — this represents what the user's subgroup is owed/owes, which is the
        // actionable figure for the user (intra-alias settlement is out of scope).
        var individualGroupIds = userGroups.Where(ug => !ug.Group.UseAliases).Select(ug => ug.Group.Id).ToList();
        var aliasGroupIds = userGroups.Where(ug => ug.Group.UseAliases).Select(ug => ug.Group.Id).ToList();

        // Per-user balances for individual-mode groups (existing logic)
        var paidByGroup = individualGroupIds.Count > 0
            ? await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => individualGroupIds.Contains(e.GroupId) && e.PaidBy == user.Id && e.DeletedAt == null)
                .GroupBy(e => e.GroupId)
                .Select(g => new { GroupId = g.Key, Total = g.Sum(e => e.Amount) })
                .ToDictionaryAsync(x => x.GroupId, x => x.Total)
            : new Dictionary<int, decimal>();

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

        // Alias-level balances for alias-mode groups
        // First, find the user's alias in each alias-mode group
        var userMemberships = await unitOfWork.GroupMembers
            .AsNoTracking()
            .Where(gm => aliasGroupIds.Contains(gm.GroupId) && gm.UserId == user.Id && gm.DeletedAt == null)
            .Select(gm => new { gm.GroupId, gm.AliasId })
            .ToListAsync();

        var userAliasByGroup = userMemberships
            .Where(x => x.AliasId != null)
            .ToDictionary(x => x.GroupId, x => x.AliasId!.Value);

        // TotalPaid per alias: sum of Expense.Amount where Expense.PaidByAliasId == aliasId
        // (with fallback to current-membership for null PaidByAliasId — backward compat)
        var aliasPaidByGroup = new Dictionary<(int GroupId, int AliasId), decimal>();

        if (aliasGroupIds.Count > 0)
        {
            var aliasExpenses = await unitOfWork.Expenses
                .AsNoTracking()
                .Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null)
                .Select(e => new { e.GroupId, e.PaidBy, e.PaidByAliasId, e.Amount })
                .ToListAsync();

            // Get current alias membership for fallback
            var currentMemberships = await unitOfWork.GroupMembers
                .AsNoTracking()
                .Where(gm => aliasGroupIds.Contains(gm.GroupId) && gm.DeletedAt == null && gm.AliasId != null)
                .Select(gm => new { gm.GroupId, gm.UserId, gm.AliasId })
                .ToListAsync();

            var userAliasLookup = currentMemberships
                .Where(x => x.AliasId != null)
                .ToLookup(x => (x.GroupId, x.UserId), x => x.AliasId!.Value);

            foreach (var expense in aliasExpenses)
            {
                int? aliasId;

                if (expense.PaidByAliasId != null)
                {
                    aliasId = expense.PaidByAliasId;
                }
                else
                {
                    aliasId = userAliasLookup[(expense.GroupId, expense.PaidBy)].FirstOrDefault();
                }

                if (aliasId != null)
                {
                    var key = (expense.GroupId, aliasId.Value);
                    aliasPaidByGroup.TryGetValue(key, out var current);
                    aliasPaidByGroup[key] = current + expense.Amount;
                }
            }
        }

        // TotalOwed per alias: sum of ExpenseAliasSplit.SplitAmount for that alias
        var aliasSplitByGroup = aliasGroupIds.Count > 0
            ? await unitOfWork.ExpenseAliasSplits
                .AsNoTracking()
                .Join(unitOfWork.Expenses.AsNoTracking().Where(e => aliasGroupIds.Contains(e.GroupId) && e.DeletedAt == null),
                    eas => eas.ExpenseId, e => e.Id,
                    (eas, e) => new { e.GroupId, eas.AliasId, eas.SplitAmount })
                .GroupBy(x => new { x.GroupId, x.AliasId })
                .Select(g => new { g.Key.GroupId, g.Key.AliasId, Total = g.Sum(x => x.SplitAmount) })
                .ToDictionaryAsync(x => (x.GroupId, x.AliasId), x => x.Total)
            : new Dictionary<(int GroupId, int AliasId), decimal>();

        var groupDtos = userGroups.Select(ug =>
        {
            decimal netBalance;

            if (ug.Group.UseAliases)
            {
                // Alias-mode: user's NetBalance = their alias's net balance
                if (userAliasByGroup.TryGetValue(ug.Group.Id, out var aliasId))
                {
                    var totalPaid = aliasPaidByGroup.GetValueOrDefault((ug.Group.Id, aliasId), 0m);
                    var totalOwed = aliasSplitByGroup.GetValueOrDefault((ug.Group.Id, aliasId), 0m);
                    netBalance = totalPaid - totalOwed;
                }
                else
                {
                    netBalance = 0m;
                }
            }
            else
            {
                // Individual-mode: existing per-user computation
                netBalance = paidByGroup.GetValueOrDefault(ug.Group.Id, 0m)
                             - splitByGroup.GetValueOrDefault(ug.Group.Id, 0m);
            }

            return new GroupDto
            {
                Id = ug.Group.Guid.ToString(),
                OriginalId = ug.Group.Id,
                Name = ug.Group.Name,
                Description = ug.Group.Description,
                CreatedByUserId = ug.Group.CreatedByUser?.Guid.ToString() ?? "",
                MemberCount = ug.MemberCount,
                CreatedAt = ug.Group.CreatedAt,
                UpdatedAt = ug.Group.UpdatedAt,
                UseAliases = ug.Group.UseAliases,
                AliasSetupFinalized = ug.Group.AliasSetupFinalized,
                NetBalance = netBalance
            };
        }).ToList();

        return Result<List<GroupDto>>.Success(groupDtos);
    }

    public async Task<Result<GroupDto>> CreateGroupAsync(Guid currentUserId, CreateGroupRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized(loc["UserNotFound"]);

        var group = new Group
        {
            Name = request.Name,
            Description = request.Description,
            CreatedBy = user.Id,
            UseAliases = request.UseAliases,
        };

        var creatorMember = new GroupMember
        {
            UserId = user.Id,
            Role = GroupRole.Admin
        };

        group.GroupMembers.Add(creatorMember);

        // If alias mode is enabled, auto-create a singleton alias for the creator.
        // Uses navigation relationships so EF fixup assigns the FKs on save
        // (group.Id is 0 until SaveChangesAsync; setting GroupId/AliasId directly would violate FKs).
        if (request.UseAliases)
        {
            var singletonName = await AliasNamingHelper.GenerateUniqueSingletonNameAsync(
                unitOfWork, group.Id, user);

            var singletonAlias = new Alias
            {
                Name = singletonName,
                IsSingleton = true
            };

            group.Aliases.Add(singletonAlias);
            creatorMember.Alias = singletonAlias;
        }

        unitOfWork.Groups.Add(group);

        var groupDto = new GroupDto
        {
            Id = group.Guid.ToString(),
            OriginalId = group.Id,
            Name = group.Name,
            Description = group.Description,
            CreatedByUserId = user.Guid.ToString(),
            MemberCount = group.GroupMembers.Count,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            UseAliases = group.UseAliases,
            AliasSetupFinalized = group.AliasSetupFinalized
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result<GroupDto>> GetGroupAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .Include(g => g.CreatedByUser)
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<GroupDto>.Forbidden(loc["AccessNotAllowed"]);

        var memberCount = await unitOfWork.GroupMembers
            .CountAsync(gm => gm.GroupId == group.Id && gm.DeletedAt == null);

        var groupDto = new GroupDto
        {
            Id = group.Guid.ToString(),
            OriginalId = group.Id,
            Name = group.Name,
            Description = group.Description,
            CreatedByUserId = group.CreatedByUser?.Guid.ToString() ?? "",
            MemberCount = memberCount,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            UseAliases = group.UseAliases,
            AliasSetupFinalized = group.AliasSetupFinalized
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result<GroupDto>> UpdateGroupAsync(string groupId, Guid currentUserId,
        UpdateGroupRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .Include(g => g.CreatedByUser)
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupDto>.NotFound(loc["GroupNotFound"]);

        // Check if user is admin of the group
        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result<GroupDto>.Forbidden(loc["AccessNotAllowed"]);

        if (groupMember.Role != GroupRole.Admin)
            return Result<GroupDto>.Forbidden(loc["OnlyAdminsCanUpdateGroup"]);

        // Update group properties
        // UseAliases is immutable — the DTO (UpdateGroupRequestDto) intentionally has no
        // UseAliases field, so JSON deserialization drops any client-supplied value.
        if (!string.IsNullOrWhiteSpace(request.Name))
            group.Name = request.Name;

        if (request.Description != null)
            group.Description = request.Description;

        var memberCount = await unitOfWork.GroupMembers
            .CountAsync(gm => gm.GroupId == group.Id && gm.DeletedAt == null);

        var groupDto = new GroupDto
        {
            Id = group.Guid.ToString(),
            OriginalId = group.Id,
            Name = group.Name,
            Description = group.Description,
            CreatedByUserId = group.CreatedByUser?.Guid.ToString() ?? "",
            MemberCount = memberCount,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            UseAliases = group.UseAliases,
            AliasSetupFinalized = group.AliasSetupFinalized
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result> DeleteGroupAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound(loc["GroupNotFound"]);

        // Check if user is admin of the group
        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        if (groupMember.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyAdminsCanDeleteGroup"]);

        // Soft delete the group
        group.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Soft delete all group members and notify non-deleters
        var members = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .ToListAsync();

        foreach (var member in members)
        {
            member.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

            if (member.UserId == user.Id) continue;

            // Use the recipient's UiLanguage for the email (AC-005).
            var memberLanguage = SupportedLanguages.Normalize(member.User.Settings.UiLanguage);
            await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupDeletedModel
            {
                To = member.User.Email, RecipientFirstName = member.User.FirstName,
                DeletedByFirstName = user.FirstName, DeletedByLastName = user.LastName,
                GroupName = group.Name
            }, memberLanguage));
        }

        return Result.Success();
    }

    public async Task<Result<List<GroupMemberDto>>> GetGroupMembersAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<GroupMemberDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<GroupMemberDto>>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<GroupMemberDto>>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<GroupMemberDto>>.Forbidden(loc["AccessNotAllowed"]);

        var members = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .Where(gm => gm.User.DeletedAt == null)
            .OrderBy(gm => gm.CreatedAt)
            .ToListAsync();

        var memberDtos = members.Select(member => new GroupMemberDto
        {
            GroupId = group.Guid.ToString(),
            UserId = member.User.Guid.ToString(),
            User = new UserInfoDto
            {
                Id = member.User.Guid.ToString(),
                Email = member.User.Email,
                FirstName = member.User.FirstName,
                LastName = member.User.LastName
            },
            Role = member.Role.ToString().ToLowerInvariant(),
            JoinedAt = member.CreatedAt
        }).ToList();

        return Result<List<GroupMemberDto>>.Success(memberDtos);
    }

    public async Task<Result<GroupMemberDto>> AddGroupMemberAsync(string groupId, Guid currentUserId,
        AddGroupMemberRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupMemberDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<GroupMemberDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupMemberDto>.NotFound(loc["GroupNotFound"]);

        // Check if current user is admin of the group
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<GroupMemberDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<GroupMemberDto>.Forbidden(loc["OnlyAdminsCanAddMembers"]);

        // Find user to be added
        var userToAdd = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.UserEmail && u.DeletedAt == null);

        if (userToAdd == null)
            return Result<GroupMemberDto>.NotFound(loc["UserWithEmailNotFound"]);

        // Check if user is already a member
        var existingMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == userToAdd.Id && gm.DeletedAt == null);

        if (existingMembership != null)
            return Result<GroupMemberDto>.Conflict(loc["UserAlreadyMember"]);

        // Parse role
        if (!Enum.TryParse<GroupRole>(request.Role, true, out var role))
            role = GroupRole.Member;

        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = userToAdd.Id,
            Role = role
        };

        unitOfWork.GroupMembers.Add(groupMember);

        // If the group is alias-mode, auto-create a singleton alias for the new member.
        // This applies whether or not the group is finalized (REQ-007 / AC-012).
        // Uses navigation relationships so EF fixup assigns the FK on save
        // (singletonAlias.Id is 0 until SaveChangesAsync; setting AliasId directly would violate the FK).
        if (group.UseAliases)
        {
            var singletonName = await AliasNamingHelper.GenerateUniqueSingletonNameAsync(
                unitOfWork, group.Id, userToAdd);

            var singletonAlias = new Alias
            {
                GroupId = group.Id,
                Name = singletonName,
                IsSingleton = true
            };

            unitOfWork.Aliases.Add(singletonAlias);
            groupMember.Alias = singletonAlias;
        }

        var memberDto = new GroupMemberDto
        {
            GroupId = group.Guid.ToString(),
            UserId = userToAdd.Guid.ToString(),
            User = new UserInfoDto
            {
                Id = userToAdd.Guid.ToString(),
                Email = userToAdd.Email,
                FirstName = userToAdd.FirstName,
                LastName = userToAdd.LastName
            },
            Role = role.ToString().ToLowerInvariant(),
            JoinedAt = groupMember.CreatedAt
        };

        return Result<GroupMemberDto>.Success(memberDto);
    }

    public async Task<Result> RemoveGroupMemberAsync(string groupId, string userId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(userId, out var userGuid))
            return Result.BadRequest(loc["InvalidUserIdFormat"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound(loc["GroupNotFound"]);

        var userToRemove = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (userToRemove == null)
            return Result.NotFound(loc["UserToRemoveNotFound"]);

        // Check if current user is admin of the group or removing themselves
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        var membershipToRemove = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == userToRemove.Id && gm.DeletedAt == null);

        if (membershipToRemove == null)
            return Result.NotFound(loc["UserNotMemberOfGroup"]);

        // Allow user to remove themselves or admin to remove any member
        var canRemove = currentUser.Id == userToRemove.Id || currentUserMembership.Role == GroupRole.Admin;

        if (!canRemove)
            return Result.Forbidden(loc["RemoveMemberNotAllowed"]);

        // Don't allow removing the only admin
        if (membershipToRemove.Role == GroupRole.Admin)
        {
            var adminCount = await unitOfWork.GroupMembers
                .CountAsync(gm => gm.GroupId == group.Id && gm.RoleId == (int)GroupRole.Admin && gm.DeletedAt == null);

            if (adminCount <= 1)
                return Result.Conflict(loc["CannotRemoveOnlyAdmin"]);
        }

        // Soft delete the membership
        membershipToRemove.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        // Handle alias side: if the member had an AliasId, clear it and check
        // if the alias now has zero non-deleted members (soft-delete it if so).
        // Historical ExpenseAliasSplit rows keep their AliasId (do not touch).
        if (membershipToRemove.AliasId != null)
        {
            var aliasId = membershipToRemove.AliasId.Value;
            membershipToRemove.AliasId = null;

            var remainingMemberCount = await unitOfWork.GroupMembers
                .CountAsync(gm => gm.GroupId == group.Id && gm.AliasId == aliasId && gm.DeletedAt == null);

            if (remainingMemberCount == 0)
            {
                var alias = await unitOfWork.Aliases
                    .FirstOrDefaultAsync(a => a.Id == aliasId && a.DeletedAt == null);

                if (alias != null)
                {
                    alias.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
                }
            }
        }

        // Notify the removed user only if it's not a self-removal
        if (currentUser.Id != userToRemove.Id)
        {
            // Use the removed user's UiLanguage for the email (AC-005).
            var removedUserLanguage = SupportedLanguages.Normalize(userToRemove.Settings.UiLanguage);
            await notificationService.EnqueueAsync(emailTemplateProvider.Render(new GroupMemberRemovedModel
            {
                To = userToRemove.Email, RecipientFirstName = userToRemove.FirstName,
                RemovedByFirstName = currentUser.FirstName, RemovedByLastName = currentUser.LastName,
                GroupName = group.Name
            }, removedUserLanguage));
        }

        return Result.Success();
    }

    public async Task<Result<GroupMemberDto>> ChangeMemberRoleAsync(string groupId, string userId,
        Guid currentUserId, UpdateGroupMemberRoleRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupMemberDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        if (!Guid.TryParse(userId, out var userGuid))
            return Result<GroupMemberDto>.BadRequest(loc["InvalidUserIdFormat"]);

        if (!Enum.TryParse<GroupRole>(request.Role, true, out var newRole))
            return Result<GroupMemberDto>.BadRequest(loc["InvalidRoleValue"]);

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<GroupMemberDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupMemberDto>.NotFound(loc["GroupNotFound"]);

        // Check if caller is a member of the group
        var callerMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (callerMember == null)
            return Result<GroupMemberDto>.Forbidden(loc["AccessNotAllowed"]);

        if (callerMember.Role != GroupRole.Admin)
            return Result<GroupMemberDto>.Forbidden(loc["OnlyAdminsCanChangeRoles"]);

        // Find target user
        var targetUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (targetUser == null)
            return Result<GroupMemberDto>.NotFound(loc["UserNotFound"]);

        // Find target's membership
        var targetMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == targetUser.Id && gm.DeletedAt == null);

        if (targetMember == null)
            return Result<GroupMemberDto>.NotFound(loc["UserNotMemberOfGroup"]);

        // Cannot change own role
        if (targetMember.UserId == callerMember.UserId)
            return Result<GroupMemberDto>.Forbidden(loc["CannotChangeOwnRole"]);

        // Already has this role
        if (targetMember.Role == newRole)
            return Result<GroupMemberDto>.BadRequest(loc["UserAlreadyHasRole"]);

        // Last-admin protection when demoting
        if (newRole == GroupRole.Member && targetMember.Role == GroupRole.Admin)
        {
            var adminCount = await unitOfWork.GroupMembers
                .CountAsync(gm => gm.GroupId == group.Id && gm.RoleId == (int)GroupRole.Admin && gm.DeletedAt == null);

            if (adminCount <= 1)
                return Result<GroupMemberDto>.Conflict(loc["CannotDemoteOnlyAdmin"]);
        }

        targetMember.Role = newRole;

        var memberDto = new GroupMemberDto
        {
            GroupId = group.Guid.ToString(),
            UserId = targetUser.Guid.ToString(),
            User = new UserInfoDto
            {
                Id = targetUser.Guid.ToString(),
                Email = targetUser.Email,
                FirstName = targetUser.FirstName,
                LastName = targetUser.LastName
            },
            Role = newRole.ToString().ToLowerInvariant(),
            JoinedAt = targetMember.CreatedAt
        };

        return Result<GroupMemberDto>.Success(memberDto);
    }

    public async Task<Result<PaginatedResponseDto<ImportStatusDto>>> GetGroupImportsAsync(
        string groupId,
        Guid currentUserId,
        int page = 1,
        int limit = 20)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<ImportStatusDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<PaginatedResponseDto<ImportStatusDto>>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<ImportStatusDto>>.NotFound(loc["GroupNotFound"]);

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<ImportStatusDto>>.Forbidden(loc["AccessNotAllowed"]);

        // Get total count
        var totalCount = await unitOfWork.Imports
            .CountAsync(i => i.GroupId == group.Id);

        // Get paginated imports
        var imports = await unitOfWork.Imports
            .Where(i => i.GroupId == group.Id)
            .Include(i => i.Group)
            .Include(i => i.User)
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        var importDtos = imports.Select(import => new ImportStatusDto(import)).ToList();

        var pagination = new PaginationDto
        {
            Page = page,
            Limit = limit,
            Total = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / limit),
            HasNext = page * limit < totalCount,
            HasPrev = page > 1
        };

        var paginatedResponse = PaginatedResponseDto<ImportStatusDto>.SuccessResponse(importDtos, pagination);
        return Result<PaginatedResponseDto<ImportStatusDto>>.Success(paginatedResponse);
    }

    public async Task<Result<ImportStatusDto>> GetImportStatusAsync(Guid importId, Guid currentUserId)
    {
        var import = await unitOfWork.Imports
            .Include(i => i.Group)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Guid == importId);
        if (import == null)
            return Result<ImportStatusDto>.NotFound(loc["ImportNotFound"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);
        if (user == null)
            return Result<ImportStatusDto>.Unauthorized(loc["UserNotFound"]);

        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == import.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);
        if (!isMember)
            return Result<ImportStatusDto>.Forbidden(loc["AccessNotAllowed"]);

        var isImportOwner = import.UserId == user.Id;
        if (!isImportOwner)
            return Result<ImportStatusDto>.Forbidden(loc["OnlyOwnImports"]);

        var result = new ImportStatusDto(import);
        return Result<ImportStatusDto>.Success(result);
    }
}