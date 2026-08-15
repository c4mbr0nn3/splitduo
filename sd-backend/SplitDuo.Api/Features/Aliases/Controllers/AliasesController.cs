using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Aliases.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Aliases.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class AliasesController(
    IAliasesService aliasesService,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator) : BaseApiController
{
    [HttpGet("groups/{groupId}/aliases")]
    public async Task<ActionResult<ApiResponseDto<List<AliasDto>>>> ListAliases(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<AliasDto>>());

        var result = await aliasesService.ListAliasesAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Aliases retrieved successfully");
    }

    [HttpPost("groups/{groupId}/aliases")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> CreateAlias(string groupId,
        [FromBody] CreateAliasRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<AliasDto>());

        var result = await aliasesService.CreateAliasAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Alias created successfully");
    }

    [HttpPost("groups/{groupId}/aliases/finalize")]
    public async Task<ActionResult> FinalizeAliasSetup(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await aliasesService.FinalizeAliasSetupAsync(groupId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Alias setup finalized successfully");
    }

    [HttpPut("aliases/{aliasId}")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> UpdateAlias(string aliasId,
        [FromBody] UpdateAliasRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<AliasDto>());

        var result = await aliasesService.UpdateAliasAsync(aliasId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(result.Value!.GroupId);
        }

        return HandleResult(result, "Alias updated successfully");
    }

    [HttpDelete("aliases/{aliasId}")]
    public async Task<ActionResult> DeleteAlias(string aliasId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        // Look up the alias's group before deletion for cache invalidation
        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Guid.ToString() == aliasId);
        var groupGuid = alias?.Group != null ? alias.Group.Guid.ToString() : null;

        var result = await aliasesService.DeleteAliasAsync(aliasId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            if (groupGuid != null)
                await cacheInvalidator.InvalidateGroupAsync(groupGuid);
        }

        return HandleResult(result, "Alias deleted successfully");
    }

    [HttpPost("aliases/{aliasId}/members")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> AssignMember(string aliasId,
        [FromBody] AssignAliasMemberRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<AliasDto>());

        var result = await aliasesService.AssignMemberAsync(aliasId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(result.Value!.GroupId);
        }

        return HandleResult(result, "Member assigned to alias successfully");
    }

    [HttpDelete("aliases/{aliasId}/members/{userId}")]
    public async Task<ActionResult> RemoveMember(string aliasId, string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        // Look up the alias's group before removal for cache invalidation
        var alias = await unitOfWork.Aliases
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Guid.ToString() == aliasId);
        var groupGuid = alias?.Group != null ? alias.Group.Guid.ToString() : null;

        var result = await aliasesService.RemoveMemberAsync(aliasId, userId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            if (groupGuid != null)
                await cacheInvalidator.InvalidateGroupAsync(groupGuid);
        }

        return HandleResult(result, "Member removed from alias successfully");
    }
}
