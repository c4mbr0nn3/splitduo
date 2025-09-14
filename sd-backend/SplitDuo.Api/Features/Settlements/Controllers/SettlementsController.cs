using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Api.Features.Settlements.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Settlements.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/settlements")]
[Authorize]
public class SettlementsController(ISettlementsService settlementsService, IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponseDto<SettlementDto>>> GetGroupSettlements(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(
                Result<PaginatedResponseDto<SettlementDto>>.Unauthorized("User not authenticated"));

        var result =
            await settlementsService.GetGroupSettlementsAsync(groupId, currentUserId.Value, page, limit, startDate,
                endDate);
        return HandlePaginatedResult(result, "Settlements retrieved successfully");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> CreateSettlement(string groupId,
        [FromBody] CreateSettlementRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<SettlementDto>.Unauthorized("User not authenticated"));

        var result = await settlementsService.CreateSettlementAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Settlement created successfully");
    }

    [HttpPut("{settlementId}")]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> UpdateSettlement(string groupId, string settlementId,
        [FromBody] UpdateSettlementRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<SettlementDto>.Unauthorized("User not authenticated"));

        var result =
            await settlementsService.UpdateSettlementAsync(groupId, settlementId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Settlement updated successfully");
    }

    [HttpDelete("{settlementId}")]
    public async Task<ActionResult> DeleteSettlement(string groupId, string settlementId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await settlementsService.DeleteSettlementAsync(groupId, settlementId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Settlement deleted successfully");
    }
}