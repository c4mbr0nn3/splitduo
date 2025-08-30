using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.ImportExport.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.ImportExport.Controllers;

[ApiController]
[Authorize]
public class ImportExportController : ControllerBase
{
    [HttpPost("api/v1/groups/{groupId}/import")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportData(string groupId, [FromForm] ImportRequestDto request)
    {
        // TODO: Implement import data backup logic
        throw new NotImplementedException();
    }

    [HttpGet("api/v1/groups/{groupId}/export/csv")]
    public async Task<ActionResult> ExportToCsv(string groupId, [FromQuery] ExportRequestDto request)
    {
        // TODO: Implement export to CSV logic
        throw new NotImplementedException();
    }

    [HttpGet("api/v1/groups/{groupId}/export/cospend")]
    public async Task<ActionResult> ExportToCospend(string groupId, [FromQuery] ExportRequestDto request)
    {
        // TODO: Implement export to Cospend format logic
        throw new NotImplementedException();
    }

    [HttpGet("api/v1/imports/{importId}/status")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> GetImportStatus(string importId)
    {
        // TODO: Implement get import status logic
        throw new NotImplementedException();
    }
}