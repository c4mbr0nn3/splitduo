using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Services.Exports;

namespace SplitDuo.Api.Features.Exports.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ExportsController(IExportsService exportsService, IGroupsService groupsService) : BaseApiController
{
    [HttpGet("groups/{groupId}/export/csv")]
    public async Task<ActionResult> ExportToCsv(string groupId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return HandleResult(Result.Unauthorized("User not authenticated"));
        }

        // Verify user has access to the group
        var groupResult = await groupsService.GetGroupAsync(groupId, user.Guid);
        if (groupResult.IsFailure)
        {
            return HandleResult(groupResult.ToResult());
        }

        var group = groupResult.Value;
        var exportResult = await exportsService.ExportToCsvAsync(group!.OriginalId);

        if (exportResult.IsFailure)
        {
            return HandleResult(exportResult.ToResult());
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"splitduo-export-{groupId}-{timestamp}.csv";

        return File(exportResult.Value!, "text/csv", fileName);
    }
}