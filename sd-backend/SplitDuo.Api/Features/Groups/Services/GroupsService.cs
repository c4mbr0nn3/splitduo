using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
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

    Task<Result<PaginatedResponseDto<ImportStatusDto>>> GetGroupImportsAsync(
        string groupId,
        Guid currentUserId,
        int page = 1,
        int limit = 20);

    Task<Result<ImportStatusDto>> GetImportStatusAsync(Guid importId, Guid currentUserId);
}

public class GroupsService(IUnitOfWork unitOfWork, INotificationService notificationService) : IGroupsService
{
    public async Task<Result<List<GroupDto>>> GetUserGroupsAsync(Guid currentUserId, int? limit = null)
    {
        var user = await unitOfWork.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<GroupDto>>.Unauthorized("User not found");

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

        var paidByGroup = await unitOfWork.Expenses
            .AsNoTracking()
            .Where(e => groupIds.Contains(e.GroupId) && e.PaidBy == user.Id && e.DeletedAt == null)
            .GroupBy(e => e.GroupId)
            .Select(g => new { GroupId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.GroupId, x => x.Total);

        var splitByGroup = await unitOfWork.ExpenseSplits
            .AsNoTracking()
            .Where(es => es.UserId == user.Id)
            .Join(unitOfWork.Expenses.AsNoTracking().Where(e => e.DeletedAt == null),
                es => es.ExpenseId, e => e.Id,
                (es, e) => new { e.GroupId, es.SplitAmount })
            .Where(x => groupIds.Contains(x.GroupId))
            .GroupBy(x => x.GroupId)
            .Select(g => new { GroupId = g.Key, Total = g.Sum(x => x.SplitAmount) })
            .ToDictionaryAsync(x => x.GroupId, x => x.Total);

        var groupDtos = userGroups.Select(ug => new GroupDto
        {
            Id = ug.Group.Guid.ToString(),
            OriginalId = ug.Group.Id,
            Name = ug.Group.Name,
            Description = ug.Group.Description,
            CreatedByUserId = ug.Group.CreatedByUser?.Guid.ToString() ?? "",
            MemberCount = ug.MemberCount,
            CreatedAt = ug.Group.CreatedAt,
            UpdatedAt = ug.Group.UpdatedAt,
            NetBalance = paidByGroup.GetValueOrDefault(ug.Group.Id, 0m)
                         - splitByGroup.GetValueOrDefault(ug.Group.Id, 0m)
        }).ToList();

        return Result<List<GroupDto>>.Success(groupDtos);
    }

    public async Task<Result<GroupDto>> CreateGroupAsync(Guid currentUserId, CreateGroupRequestDto request)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized("User not found");

        var group = new Group
        {
            Name = request.Name,
            Description = request.Description,
            CreatedBy = user.Id,
        };

        group.GroupMembers.Add(new GroupMember
        {
            UserId = user.Id,
            Role = GroupRole.Admin
        });

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
            UpdatedAt = group.UpdatedAt
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result<GroupDto>> GetGroupAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupDto>.BadRequest("Invalid group ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .Include(g => g.CreatedByUser)
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupDto>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<GroupDto>.Forbidden("Access to this group is not allowed");

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
            UpdatedAt = group.UpdatedAt
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result<GroupDto>> UpdateGroupAsync(string groupId, Guid currentUserId,
        UpdateGroupRequestDto request)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<GroupDto>.BadRequest("Invalid group ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<GroupDto>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .Include(g => g.CreatedByUser)
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupDto>.NotFound("Group not found");

        // Check if user is admin of the group
        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result<GroupDto>.Forbidden("Access to this group is not allowed");

        if (groupMember.Role != GroupRole.Admin)
            return Result<GroupDto>.Forbidden("Only group administrators can update group information");

        // Update group properties
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
            UpdatedAt = group.UpdatedAt
        };

        return Result<GroupDto>.Success(groupDto);
    }

    public async Task<Result> DeleteGroupAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result.BadRequest("Invalid group ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound("Group not found");

        // Check if user is admin of the group
        var groupMember = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (groupMember == null)
            return Result.Forbidden("Access to this group is not allowed");

        if (groupMember.Role != GroupRole.Admin)
            return Result.Forbidden("Only group administrators can delete the group");

        // Soft delete the group
        group.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Soft delete all group members and notify non-deleters
        var members = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
            .Include(gm => gm.User)
            .ToListAsync();

        foreach (var member in members)
        {
            member.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (member.UserId == user.Id) continue;

            await notificationService.EnqueueAsync(new Notification
            {
                To = member.User.Email,
                Subject = $"{group.Name} has been deleted",
                Body = CreateGroupDeletedEmailBody(member.User, group, user)
            });
        }

        return Result.Success();
    }

    public async Task<Result<List<GroupMemberDto>>> GetGroupMembersAsync(string groupId, Guid currentUserId)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<List<GroupMemberDto>>.BadRequest("Invalid group ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<GroupMemberDto>>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<List<GroupMemberDto>>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<List<GroupMemberDto>>.Forbidden("Access to this group is not allowed");

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
            return Result<GroupMemberDto>.BadRequest("Invalid group ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result<GroupMemberDto>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<GroupMemberDto>.NotFound("Group not found");

        // Check if current user is admin of the group
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result<GroupMemberDto>.Forbidden("Access to this group is not allowed");

        if (currentUserMembership.Role != GroupRole.Admin)
            return Result<GroupMemberDto>.Forbidden("Only group administrators can add members");

        // Find user to be added
        var userToAdd = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Email == request.UserEmail && u.DeletedAt == null);

        if (userToAdd == null)
            return Result<GroupMemberDto>.NotFound("User with this email not found");

        // Check if user is already a member
        var existingMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == userToAdd.Id && gm.DeletedAt == null);

        if (existingMembership != null)
            return Result<GroupMemberDto>.Conflict("User is already a member of this group");

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
            return Result.BadRequest("Invalid group ID format");

        if (!Guid.TryParse(userId, out var userGuid))
            return Result.BadRequest("Invalid user ID format");

        var currentUser = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (currentUser == null)
            return Result.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result.NotFound("Group not found");

        var userToRemove = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userGuid && u.DeletedAt == null);

        if (userToRemove == null)
            return Result.NotFound("User to remove not found");

        // Check if current user is admin of the group or removing themselves
        var currentUserMembership = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == currentUser.Id && gm.DeletedAt == null);

        if (currentUserMembership == null)
            return Result.Forbidden("Access to this group is not allowed");

        var membershipToRemove = await unitOfWork.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == userToRemove.Id && gm.DeletedAt == null);

        if (membershipToRemove == null)
            return Result.NotFound("User is not a member of this group");

        // Allow user to remove themselves or admin to remove any member
        var canRemove = currentUser.Id == userToRemove.Id || currentUserMembership.Role == GroupRole.Admin;

        if (!canRemove)
            return Result.Forbidden("You can only remove yourself or, as an admin, remove other members");

        // Don't allow removing the only admin
        if (membershipToRemove.Role == GroupRole.Admin)
        {
            var adminCount = await unitOfWork.GroupMembers
                .CountAsync(gm => gm.GroupId == group.Id && gm.RoleId == (int)GroupRole.Admin && gm.DeletedAt == null);

            if (adminCount <= 1)
                return Result.Conflict("Cannot remove the only administrator of the group");
        }

        // Soft delete the membership
        membershipToRemove.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Notify the removed user only if it's not a self-removal
        if (currentUser.Id != userToRemove.Id)
        {
            await notificationService.EnqueueAsync(new Notification
            {
                To = userToRemove.Email,
                Subject = $"You've been removed from {group.Name}",
                Body = CreateRemovedFromGroupEmailBody(userToRemove, group, currentUser)
            });
        }

        return Result.Success();
    }

    public async Task<Result<PaginatedResponseDto<ImportStatusDto>>> GetGroupImportsAsync(
        string groupId,
        Guid currentUserId,
        int page = 1,
        int limit = 20)
    {
        if (!Guid.TryParse(groupId, out var groupGuid))
            return Result<PaginatedResponseDto<ImportStatusDto>>.BadRequest("Invalid group ID format");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<PaginatedResponseDto<ImportStatusDto>>.Unauthorized("User not found");

        var group = await unitOfWork.Groups
            .FirstOrDefaultAsync(g => g.Guid == groupGuid && g.DeletedAt == null);

        if (group == null)
            return Result<PaginatedResponseDto<ImportStatusDto>>.NotFound("Group not found");

        // Check if user is member of the group
        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == group.Id && gm.UserId == user.Id && gm.DeletedAt == null);

        if (!isMember)
            return Result<PaginatedResponseDto<ImportStatusDto>>.Forbidden("Access to this group is not allowed");

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
            return Result<ImportStatusDto>.NotFound("Import not found");

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);
        if (user == null)
            return Result<ImportStatusDto>.Unauthorized("User not found");

        var isMember = await unitOfWork.GroupMembers
            .AnyAsync(gm => gm.GroupId == import.GroupId && gm.UserId == user.Id && gm.DeletedAt == null);
        if (!isMember)
            return Result<ImportStatusDto>.Forbidden("Access to this group is not allowed");

        var isImportOwner = import.UserId == user.Id;
        if (!isImportOwner)
            return Result<ImportStatusDto>.Forbidden("You can only view the status of your own imports");

        var result = new ImportStatusDto(import);
        return Result<ImportStatusDto>.Success(result);
    }

    private static string CreateGroupDeletedEmailBody(User recipient, Group group, User deletedBy)
    {
        return $"""
                <p>Hello {recipient.FirstName},</p>
                <p>{deletedBy.FirstName} {deletedBy.LastName} has deleted the group <strong>{group.Name}</strong>.</p>
                <p>All expenses and history for this group are no longer accessible.</p>
                <p>Best regards,<br>The SplitDuo Team</p>
                """;
    }

    private static string CreateRemovedFromGroupEmailBody(User recipient, Group group, User removedBy)
    {
        return $"""
                <p>Hello {recipient.FirstName},</p>
                <p>{removedBy.FirstName} {removedBy.LastName} has removed you from the group <strong>{group.Name}</strong>.</p>
                <p>You no longer have access to this group's expenses.</p>
                <p>Best regards,<br>The SplitDuo Team</p>
                """;
    }
}