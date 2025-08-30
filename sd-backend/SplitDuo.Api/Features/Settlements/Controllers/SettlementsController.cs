using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Settlements.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/settlements")]
[Authorize]
public class SettlementsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponseDto<SettlementDto>>> GetGroupSettlements(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null)
    {
        // TODO: Implement get group settlements logic
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> CreateSettlement(string groupId, [FromBody] CreateSettlementRequestDto request)
    {
        // TODO: Implement create settlement logic
        throw new NotImplementedException();
    }

    [HttpPut("{settlementId}")]
    public async Task<ActionResult<ApiResponseDto<SettlementDto>>> UpdateSettlement(string groupId, string settlementId, [FromBody] UpdateSettlementRequestDto request)
    {
        // TODO: Implement update settlement logic
        throw new NotImplementedException();
    }

    [HttpDelete("{settlementId}")]
    public async Task<ActionResult<ApiResponseDto<object>>> DeleteSettlement(string groupId, string settlementId)
    {
        // TODO: Implement delete settlement logic
        throw new NotImplementedException();
    }
}