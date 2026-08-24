using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Aliases.Services;

public class AliasesService(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<AliasesService> loc) : IAliasesService
{
    public async Task<Result<List<AliasDto>>> ListAliasesAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<AliasDto>>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<AliasDto>>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<AliasDto>>.NotFound(loc["GroupNotFound"]);

        // Membership check — any member can view aliases
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<AliasDto>>.Forbidden(loc["AccessNotAllowed"]);

        var aliases = await unitOfWork.Aliases
            .Where(a => a.GroupId == group.Id && a.DeletedAt == null)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .ToListAsync();

        var aliasDtos = aliases.Select(a => new AliasDto(a, a.Members.Where(m => m.DeletedAt == null).ToList())).ToList();

        // Batch avatar lookup for all alias members
        var aliasMemberIds = aliases
            .SelectMany(a => a.Members.Where(m => m.DeletedAt == null))
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var avatarUserIds = await unitOfWork.UserAvatars
            .Where(a => aliasMemberIds.Contains(a.UserId))
            .Select(a => a.UserId)
            .ToHashSetAsync();

        var avatarByUserGuid = aliases
            .SelectMany(a => a.Members.Where(m => m.DeletedAt == null))
            .Where(m => avatarUserIds.Contains(m.UserId))
            .Select(m => m.User.Guid)
            .ToHashSet();

        foreach (var dto in aliasDtos)
        {
            foreach (var member in dto.Members)
            {
                member.HasAvatar = avatarByUserGuid.Contains(Guid.Parse(member.Id));
            }
        }

        return Result<List<AliasDto>>.Success(aliasDtos);
    }

    public async Task<Result<AliasDto>> CreateAliasAsync(string groupId, Guid currentUserId, CreateAliasRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<AliasDto>.BadRequest(loc["InvalidGroupIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<AliasDto>.Unauthorized(loc["UserNotFound"]);

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<AliasDto>.NotFound(loc["GroupNotFound"]);

        // Admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<AliasDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<AliasDto>.Forbidden(loc["OnlyAdminsCanManageAliases"]);

        // Reject if alias mode is not enabled
        if (!group.UseAliases)
            return Result<AliasDto>.Conflict(loc["AliasModeNotEnabled"]);

        // Validate name uniqueness among non-deleted aliases in the group
        var nameExists = await unitOfWork.Aliases
            .AnyAsync(a => a.GroupId == group.Id && a.DeletedAt == null && a.Name == request.Name);

        if (nameExists)
            return Result<AliasDto>.Conflict(loc["AliasNameAlreadyExists"]);

        var alias = new Alias
        {
            GroupId = group.Id,
            Name = request.Name,
            IsSingleton = false
        };

        unitOfWork.Aliases.Add(alias);

        // Set navigation for DTO mapping (GroupId in AliasDto reads from alias.Group)
        alias.Group = group;

        var aliasDto = new AliasDto(alias);
        return Result<AliasDto>.Success(aliasDto);
    }

    public async Task<Result<AliasDto>> UpdateAliasAsync(string aliasId, Guid currentUserId, UpdateAliasRequestDto request)
    {
        if (!Guid.TryParse(aliasId, out var aliasGuid))
            return Result<AliasDto>.BadRequest(loc["InvalidAliasIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<AliasDto>.Unauthorized(loc["UserNotFound"]);

        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

        if (alias == null)
            return Result<AliasDto>.NotFound(loc["AliasNotFound"]);

        // Admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<AliasDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<AliasDto>.Forbidden(loc["OnlyAdminsCanManageAliases"]);

        // Reject if group not alias-mode
        if (!alias.Group.UseAliases)
            return Result<AliasDto>.Conflict(loc["AliasModeNotEnabled"]);

        // If name provided, validate uniqueness among non-deleted aliases in the same group (excluding self)
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var nameExists = await unitOfWork.Aliases
                .AnyAsync(a => a.GroupId == alias.GroupId && a.DeletedAt == null && a.Name == request.Name && a.Id != alias.Id);

            if (nameExists)
                return Result<AliasDto>.Conflict(loc["AliasNameAlreadyExists"]);

            alias.Name = request.Name;
            // Rename promotes singleton to named (AC-013)
            alias.IsSingleton = false;
        }

        var aliasDto = new AliasDto(alias, alias.Members.Where(m => m.DeletedAt == null).ToList());
        await SetMembersHasAvatarAsync(aliasDto);
        return Result<AliasDto>.Success(aliasDto);
    }

    public async Task<Result> DeleteAliasAsync(string aliasId, Guid currentUserId)
    {
        if (!Guid.TryParse(aliasId, out var aliasGuid))
            return Result.BadRequest(loc["InvalidAliasIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

        if (alias == null)
            return Result.NotFound(loc["AliasNotFound"]);

        // Admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyAdminsCanManageAliases"]);

        // Reject if group not alias-mode
        if (!alias.Group.UseAliases)
            return Result.Conflict(loc["AliasModeNotEnabled"]);

        // For each current member of the alias (non-deleted GroupMembers), create a new singleton alias
        // and reassign the member to it. Uses navigation so EF fixup assigns the FK on save
        // (singletonAlias.Id is 0 until SaveChangesAsync; setting AliasId directly would violate the FK).
        var activeMembers = alias.Members.Where(m => m.DeletedAt == null).ToList();

        foreach (var member in activeMembers)
        {
            var singletonName = await AliasNamingHelper.GenerateUniqueSingletonNameAsync(
                unitOfWork, alias.GroupId, member.User);

            var singletonAlias = new Alias
            {
                GroupId = alias.GroupId,
                Name = singletonName,
                IsSingleton = true
            };

            unitOfWork.Aliases.Add(singletonAlias);

            member.Alias = singletonAlias;
        }

        // Soft-delete the alias
        alias.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        return Result.Success();
    }

    public async Task<Result<AliasDto>> AssignMemberAsync(string aliasId, Guid currentUserId, AssignAliasMemberRequestDto request)
    {
        if (!Guid.TryParse(aliasId, out var aliasGuid))
            return Result<AliasDto>.BadRequest(loc["InvalidAliasIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<AliasDto>.Unauthorized(loc["UserNotFound"]);

        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

        if (alias == null)
            return Result<AliasDto>.NotFound(loc["AliasNotFound"]);

        // Admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<AliasDto>.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<AliasDto>.Forbidden(loc["OnlyAdminsCanManageAliases"]);

        // Reject if group not alias-mode
        if (!alias.Group.UseAliases)
            return Result<AliasDto>.Conflict(loc["AliasModeNotEnabled"]);

        // Reject if alias is soft-deleted (already handled by query filter above, but double-check)
        if (alias.DeletedAt != null)
            return Result<AliasDto>.NotFound(loc["AliasNotFound"]);

        // Find the GroupMember by userId (Guid) within the same group
        if (!Guid.TryParse(request.UserId, out var memberUserGuid))
            return Result<AliasDto>.BadRequest(loc["InvalidUserIdFormat"]);

        var memberUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == memberUserGuid && u.DeletedAt == null);

        if (memberUser == null)
            return Result<AliasDto>.NotFound(loc["UserNotFound"]);

        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == memberUser.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result<AliasDto>.NotFound(loc["UserNotMemberOfGroup"]);

        // Set the member's AliasId to this alias
        groupMember.AliasId = alias.Id;

        // Build the DTO from tracked state. Re-querying the DB would miss the unsaved
        // change (the controller calls SaveChangesAsync after the service returns), and
        // EF does not auto-fixup the alias.Members navigation from a manual FK assignment.
        // Explicitly add the member to the navigation collection so the DTO reflects the
        // post-save state. memberUser is already loaded above.
        groupMember.User = memberUser;
        alias.Members.Add(groupMember);

        var updatedMembers = alias.Members.Where(m => m.DeletedAt == null).ToList();

        var aliasDto = new AliasDto(alias, updatedMembers);
        await SetMembersHasAvatarAsync(aliasDto);
        return Result<AliasDto>.Success(aliasDto);
    }

    public async Task<Result> RemoveMemberAsync(string aliasId, string userId, Guid currentUserId)
    {
        if (!Guid.TryParse(aliasId, out var aliasGuid))
            return Result.BadRequest(loc["InvalidAliasIdFormat"]);

        if (!Guid.TryParse(userId, out var userGuid))
            return Result.BadRequest(loc["InvalidUserIdFormat"]);

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.Unauthorized(loc["UserNotFound"]);

        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .Include(a => a.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(a => a.Guid == aliasGuid && a.DeletedAt == null);

        if (alias == null)
            return Result.NotFound(loc["AliasNotFound"]);

        // Admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyAdminsCanManageAliases"]);

        // Reject if group not alias-mode
        if (!alias.Group.UseAliases)
            return Result.Conflict(loc["AliasModeNotEnabled"]);

        // Find the GroupMember
        var memberUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (memberUser == null)
            return Result.NotFound(loc["UserNotFound"]);

        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == alias.GroupId && gm.UserId == memberUser.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result.NotFound(loc["UserNotMemberOfGroup"]);

        // If the member is in this alias, create a new singleton alias and reassign.
        // Uses navigation so EF fixup assigns the FK on save
        // (singletonAlias.Id is 0 until SaveChangesAsync; setting AliasId directly would violate the FK).
        if (groupMember.AliasId == alias.Id)
        {
            var singletonName = await AliasNamingHelper.GenerateUniqueSingletonNameAsync(
                unitOfWork, alias.GroupId, memberUser);

            var singletonAlias = new Alias
            {
                GroupId = alias.GroupId,
                Name = singletonName,
                IsSingleton = true
            };

            unitOfWork.Aliases.Add(singletonAlias);

            groupMember.Alias = singletonAlias;

            // Empty-check the source alias: if 0 non-deleted members remain, soft-delete it.
            // Note: the reassigned member is now tracked with the new alias, so this count
            // reflects the source alias's remaining membership after the reassignment.
            var remainingMemberCount = await unitOfWork.GroupMembers
                .CountAsync(gm => gm.GroupId == alias.GroupId && gm.AliasId == alias.Id && gm.DeletedAt == null);

            if (remainingMemberCount == 0)
            {
                alias.DeletedAt = timeProvider.GetUtcNow().ToUnixTimeSeconds();
            }
        }

        return Result.Success();
    }

    public async Task<Result> FinalizeAliasSetupAsync(string groupId, Guid currentUserId)
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

        // Membership + admin check
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden(loc["AccessNotAllowed"]);

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result.Forbidden(loc["OnlyAdminsCanFinalizeAliasSetup"]);

        // Reject if alias mode is not enabled
        if (!group.UseAliases)
            return Result.Conflict(loc["AliasModeNotEnabled"]);

        // Reject if already finalized
        if (group.AliasSetupFinalized)
            return Result.Conflict(loc["AliasSetupAlreadyFinalized"]);

        // Count non-deleted aliases with ≥2 non-deleted members
        var hasMultiPersonAlias = await unitOfWork.Aliases
            .Where(a => a.GroupId == group.Id && a.DeletedAt == null)
            .AnyAsync(a => unitOfWork.GroupMembers
                .Count(gm => gm.GroupId == a.GroupId && gm.AliasId == a.Id && gm.DeletedAt == null) >= 2);

        if (!hasMultiPersonAlias)
            return Result.Conflict(loc["MultiPersonAliasRequired"]);

        group.AliasSetupFinalized = true;

        return Result.Success();
    }

    private async Task SetMembersHasAvatarAsync(AliasDto aliasDto)
    {
        var memberGuids = aliasDto.Members
            .Select(m => Guid.Parse(m.Id))
            .ToList();

        var avatarUserGuids = await unitOfWork.UserAvatars
            .Where(a => memberGuids.Contains(a.Guid))
            .Select(a => a.Guid)
            .ToHashSetAsync();

        foreach (var member in aliasDto.Members)
        {
            member.HasAvatar = avatarUserGuids.Contains(Guid.Parse(member.Id));
        }
    }
}
