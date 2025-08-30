using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Import.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Import.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ImportController : ControllerBase
{
    [HttpPost("groups/{groupId}/import")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportData(string groupId, [FromForm] ImportRequestDto request)
    {
        // TODO: Implement import data backup logic
        throw new NotImplementedException();
    }

    [HttpGet("imports/{importId}/status")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> GetImportStatus(string importId)
    {
        // TODO: Implement get import status logic
        throw new NotImplementedException();
    }
}