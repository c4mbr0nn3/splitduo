using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Groups.Services;

public interface IGroupsService
{
    Task<Result<List<GroupDto>>> GetUserGroupsAsync(Guid currentUserId);
    Task<Result<GroupDto>> CreateGroupAsync(Guid currentUserId, CreateGroupRequestDto request);
    Task<Result<GroupDto>> GetGroupAsync(string groupId, Guid currentUserId);
    Task<Result<GroupDto>> UpdateGroupAsync(string groupId, Guid currentUserId, UpdateGroupRequestDto request);
    Task<Result> DeleteGroupAsync(string groupId, Guid currentUserId);
    Task<Result<List<GroupMemberDto>>> GetGroupMembersAsync(string groupId, Guid currentUserId);

    Task<Result<GroupMemberDto>> AddGroupMemberAsync(string groupId, Guid currentUserId,
        AddGroupMemberRequestDto request);

    Task<Result> RemoveGroupMemberAsync(string groupId, string userId, Guid currentUserId);
}

public class GroupsService(IUnitOfWork unitOfWork) : IGroupsService
{
    public async Task<Result<List<GroupDto>>> GetUserGroupsAsync(Guid currentUserId)
    {
        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null);

        if (user == null)
            return Result<List<GroupDto>>.Unauthorized("User not found");

        var userGroups = await unitOfWork.GroupMembers
            .Where(gm => gm.UserId == user.Id && gm.DeletedAt == null)
            .Include(gm => gm.Group)
            .Where(gm => gm.Group.DeletedAt == null)
            .Select(gm => new
            {
                Group = gm.Group,
                MemberCount = unitOfWork.GroupMembers
                    .Count(m => m.GroupId == gm.Group.Id && m.DeletedAt == null)
            })
            .ToListAsync();

        var groupDtos = userGroups.Select(ug => new GroupDto
        {
            Id = ug.Group.Guid.ToString(),
            Name = ug.Group.Name,
            Description = ug.Group.Description,
            CreatedByUserId = ug.Group.CreatedByUser?.Guid.ToString() ?? "",
            MemberCount = ug.MemberCount,
            CreatedAt = ug.Group.CreatedAt,
            UpdatedAt = ug.Group.UpdatedAt
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
            CreatedBy = user.Id
        };

        unitOfWork.Groups.Add(group);

        // Add creator as admin member
        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = user.Id,
            Role = GroupRole.Admin
        };

        unitOfWork.GroupMembers.Add(groupMember);

        var groupDto = new GroupDto
        {
            Id = group.Guid.ToString(),
            Name = group.Name,
            Description = group.Description,
            CreatedByUserId = user.Guid.ToString(),
            MemberCount = 1,
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

        // Soft delete all group members
        var members = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
            .ToListAsync();

        foreach (var member in members)
        {
            member.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
            Id = member.Id.ToString(),
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
            Id = groupMember.Id.ToString(),
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

        return Result.Success();
    }
}