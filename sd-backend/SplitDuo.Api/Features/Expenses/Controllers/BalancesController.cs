using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/balances")]
[Authorize]
public class BalancesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<BalanceDto>>>> GetBalances(string groupId)
    {
        // TODO: Implement get current balances logic
        throw new NotImplementedException();
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponseDto<BalanceSummaryDto>>> GetBalanceSummary(string groupId)
    {
        // TODO: Implement get balance summary logic
        throw new NotImplementedException();
    }
}