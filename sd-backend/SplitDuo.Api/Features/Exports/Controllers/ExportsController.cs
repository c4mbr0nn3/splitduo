using System.Text;
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

        // Intentionally uses DateTime.UtcNow directly (not TimeProvider): cosmetic filename
        // uniquifier with no test value — injecting TimeProvider into a controller for a filename
        // would be over-engineering.
        // Non-ASCII group names: ASP.NET's FileResult automatically emits filename*=UTF-8''...
        // for non-ASCII; the frontend reads filename= which falls back to the percent-encoded ASCII form.
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var safeName = SanitizeFileName(group!.Name, groupId);
        var fileName = $"export_{safeName}_{timestamp}.csv";

        return File(exportResult.Value!, "text/csv", fileName);
    }

    // Explicit denylist (not Path.GetInvalidFileNameChars) because the container runs Linux but
    // the downloaded file lands on a user's machine (possibly Windows). OS-independent and predictable.
    private static string SanitizeFileName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            return fallback;

        var sanitized = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c < 32 || c == '/' || c == '\\' || c == ':' || c == '*'
                || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                sanitized.Append('_');
            else
                sanitized.Append(c);
        }

        var result = sanitized.ToString().Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(result))
            return fallback;

        // Truncate so name + prefix + timestamp stays well under OS limits.
        // "export_" (7) + "_" (1) + timestamp "yyyyMMdd_HHmmss" (15) + ".csv" (4) = 27 chars overhead.
        const int maxNameLength = 150;
        return result.Length > maxNameLength ? result[..maxNameLength] : result;
    }
}