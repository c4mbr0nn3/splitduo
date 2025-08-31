using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/balances")]
[Authorize]
public class BalancesController(IBalancesService balancesService) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<BalanceDto>>>> GetBalances(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<List<BalanceDto>>.Unauthorized("User not authenticated"));

        var result = await balancesService.GetBalancesAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Balances retrieved successfully");
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponseDto<BalanceSummaryDto>>> GetBalanceSummary(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<BalanceSummaryDto>.Unauthorized("User not authenticated"));

        var result = await balancesService.GetBalanceSummaryAsync(groupId, currentUserId.Value);
        return HandleResult(result, "Balance summary retrieved successfully");
    }
}