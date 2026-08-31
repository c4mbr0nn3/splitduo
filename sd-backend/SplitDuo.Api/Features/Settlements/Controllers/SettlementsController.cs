using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Api.Features.Settlements.Services;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Settlements.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/settlements")]
[Authorize]
public class SettlementsController(
    ISettlementsService settlementsService,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator,
    ILogger<SettlementsController> logger) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<SettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedResponseDto<SettlementDto>>> GetGroupSettlements(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(NotAuthenticated<PaginatedResponseDto<SettlementDto>>());

        var result = await settlementsService.GetGroupSettlementsAsync(groupId, currentUserId.Value, page, limit);

        return HandlePaginatedResult(result, "Settlements retrieved successfully");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<SettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> CreateSettlement(string groupId,
        [FromBody] CreateSettlementRequestDto request)
    {
        logger.LogInformation("Creating settlement in group: {GroupId}", groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<SettlementDto>());

        var result = await settlementsService.CreateSettlementAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Settlement created successfully");
    }

    [HttpGet("{settlementId}")]
    [ProducesResponseType(typeof(ApiResponseDto<SettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> GetSettlement(string groupId, string settlementId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<SettlementDto>());

        var result = await settlementsService.GetSettlementAsync(groupId, settlementId, currentUserId.Value);
        return HandleResult(result, "Settlement retrieved successfully");
    }

    [HttpDelete("{settlementId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteSettlement(string groupId, string settlementId)
    {
        logger.LogWarning("Deleting settlement: {SettlementId} in group: {GroupId}", settlementId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await settlementsService.DeleteSettlementAsync(groupId, settlementId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Settlement deleted successfully");
    }
}