using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Groups.Controllers;

[ApiController]
[Route("api/v1/groups")]
[Authorize]
public class GroupsController(
    IGroupsService groupsService,
    IBalancesService balancesService,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator,
    ILogger<GroupsController> logger) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<GroupDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponseDto<List<GroupDto>>>> GetUserGroups([FromQuery] int? limit = null)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<GroupDto>>());

        var result = await groupsService.GetUserGroupsAsync(currentUserId.Value, limit);
        return HandleResult(result, "User groups retrieved successfully");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> CreateGroup([FromBody] CreateGroupRequestDto request)
    {
        logger.LogInformation("Creating group: {GroupName}", request.Name);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupDto>());

        var result = await groupsService.CreateGroupAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Group created successfully");
    }

    [HttpGet("{groupId}")]
    [ProducesResponseType(typeof(ApiResponseDto<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> GetGroup(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupDto>());

        var result = await groupsService.GetGroupAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Group retrieved successfully");
    }

    [HttpPut("{groupId}")]
    [ProducesResponseType(typeof(ApiResponseDto<GroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> UpdateGroup(string groupId,
        [FromBody] UpdateGroupRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupDto>());

        var result = await groupsService.UpdateGroupAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Group updated successfully");
    }

    [HttpDelete("{groupId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteGroup(string groupId)
    {
        logger.LogWarning("Deleting group: {GroupId}", groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await groupsService.DeleteGroupAsync(groupId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Group deleted successfully");
    }

    [HttpGet("{groupId}/stats")]
    [ProducesResponseType(typeof(ApiResponseDto<GroupStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<GroupStatsDto>>> GetGroupStats(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupStatsDto>());

        var result = await balancesService.GetGroupStatsAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Group stats retrieved successfully");
    }

    [HttpGet("{groupId}/members")]
    [ProducesResponseType(typeof(ApiResponseDto<List<GroupMemberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<List<GroupMemberDto>>>> GetGroupMembers(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<GroupMemberDto>>());

        var result = await groupsService.GetGroupMembersAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Group members retrieved successfully");
    }

    [HttpPost("{groupId}/members")]
    [ProducesResponseType(typeof(ApiResponseDto<GroupMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseDto<GroupMemberDto>>> AddGroupMember(string groupId,
        [FromBody] AddGroupMemberRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupMemberDto>());

        var result = await groupsService.AddGroupMemberAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Group member added successfully");
    }

    [HttpDelete("{groupId}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RemoveGroupMember(string groupId, string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await groupsService.RemoveGroupMemberAsync(groupId, userId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Group member removed successfully");
    }

    [HttpPut("{groupId}/members/{userId}/role")]
    [ProducesResponseType(typeof(ApiResponseDto<GroupMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseDto<GroupMemberDto>>> ChangeMemberRole(string groupId, string userId,
        [FromBody] UpdateGroupMemberRoleRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<GroupMemberDto>());

        var result = await groupsService.ChangeMemberRoleAsync(groupId, userId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Member role updated successfully");
    }
}