using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;

namespace SplitDuo.Api.Features.Groups.Controllers;

[ApiController]
[Route("api/v1/groups")]
[Authorize]
public class GroupsController(ILogger<GroupsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<GroupDto>>>> GetUserGroups()
    {
        // TODO: Implement get user groups logic
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> CreateGroup([FromBody] CreateGroupRequestDto request)
    {
        logger.LogInformation("Creating group: {GroupName}", request.Name);
        
        // TODO: Implement create group logic
        throw new NotImplementedException();
    }

    [HttpGet("{groupId}")]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> GetGroup(string groupId)
    {
        // TODO: Implement get group details logic
        throw new NotImplementedException();
    }

    [HttpPut("{groupId}")]
    public async Task<ActionResult<ApiResponseDto<GroupDto>>> UpdateGroup(string groupId, [FromBody] UpdateGroupRequestDto request)
    {
        // TODO: Implement update group logic
        throw new NotImplementedException();
    }

    [HttpDelete("{groupId}")]
    public async Task<ActionResult<ApiResponseDto<object>>> DeleteGroup(string groupId)
    {
        logger.LogWarning("Deleting group: {GroupId}", groupId);
        
        // TODO: Implement delete group logic
        throw new NotImplementedException();
    }

    [HttpGet("{groupId}/members")]
    public async Task<ActionResult<ApiResponseDto<List<GroupMemberDto>>>> GetGroupMembers(string groupId)
    {
        // TODO: Implement get group members logic
        throw new NotImplementedException();
    }

    [HttpPost("{groupId}/members")]
    public async Task<ActionResult<ApiResponseDto<GroupMemberDto>>> AddGroupMember(string groupId, [FromBody] AddGroupMemberRequestDto request)
    {
        // TODO: Implement add group member logic
        throw new NotImplementedException();
    }

    [HttpDelete("{groupId}/members/{userId}")]
    public async Task<ActionResult<ApiResponseDto<object>>> RemoveGroupMember(string groupId, string userId)
    {
        // TODO: Implement remove group member logic
        throw new NotImplementedException();
    }
}