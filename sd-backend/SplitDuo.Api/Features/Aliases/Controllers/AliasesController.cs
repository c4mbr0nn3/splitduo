using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Aliases.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Aliases.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class AliasesController(
    IAliasesService aliasesService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpGet("groups/{groupId}/aliases")]
    public async Task<ActionResult<ApiResponseDto<List<AliasDto>>>> ListAliases(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<List<AliasDto>>.Unauthorized("User not authenticated"));

        var result = await aliasesService.ListAliasesAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Aliases retrieved successfully");
    }

    [HttpPost("groups/{groupId}/aliases")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> CreateAlias(string groupId,
        [FromBody] CreateAliasRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<AliasDto>.Unauthorized("User not authenticated"));

        var result = await aliasesService.CreateAliasAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Alias created successfully");
    }

    [HttpPost("groups/{groupId}/aliases/finalize")]
    public async Task<ActionResult> FinalizeAliasSetup(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await aliasesService.FinalizeAliasSetupAsync(groupId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Alias setup finalized successfully");
    }

    [HttpPut("aliases/{aliasId}")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> UpdateAlias(string aliasId,
        [FromBody] UpdateAliasRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<AliasDto>.Unauthorized("User not authenticated"));

        var result = await aliasesService.UpdateAliasAsync(aliasId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Alias updated successfully");
    }

    [HttpDelete("aliases/{aliasId}")]
    public async Task<ActionResult> DeleteAlias(string aliasId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await aliasesService.DeleteAliasAsync(aliasId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Alias deleted successfully");
    }

    [HttpPost("aliases/{aliasId}/members")]
    public async Task<ActionResult<ApiResponseDto<AliasDto>>> AssignMember(string aliasId,
        [FromBody] AssignAliasMemberRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<AliasDto>.Unauthorized("User not authenticated"));

        var result = await aliasesService.AssignMemberAsync(aliasId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Member assigned to alias successfully");
    }

    [HttpDelete("aliases/{aliasId}/members/{userId}")]
    public async Task<ActionResult> RemoveMember(string aliasId, string userId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await aliasesService.RemoveMemberAsync(aliasId, userId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Member removed from alias successfully");
    }
}
