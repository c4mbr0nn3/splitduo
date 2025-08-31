using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Exports.Dto;

namespace SplitDuo.Api.Features.Exports.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ExportsController : ControllerBase
{
    [HttpGet("groups/{groupId}/export/csv")]
    public async Task<ActionResult> ExportToCsv(string groupId, [FromQuery] ExportRequestDto request)
    {
        // TODO: Implement export to CSV logic
        throw new NotImplementedException();
    }

    [HttpGet("groups/{groupId}/export/cospend")]
    public async Task<ActionResult> ExportToCospend(string groupId, [FromQuery] ExportRequestDto request)
    {
        // TODO: Implement export to Cospend format logic
        throw new NotImplementedException();
    }
}