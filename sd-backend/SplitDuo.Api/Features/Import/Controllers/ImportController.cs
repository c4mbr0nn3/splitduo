using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Import.Dto;
using SplitDuo.Api.Features.Import.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Import.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ImportController(
    IUnitOfWork unitOfWork,
    IGroupsService groupsService,
    IImportService importService) : BaseApiController
{
    [HttpPost("groups/{groupId}/import")]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportData(
        string groupId,
        [FromForm] ImportRequestDto request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return HandleResult(Result<ImportStatusDto>.Unauthorized("User not authenticated"));

        var groupResult = await groupsService.GetGroupAsync(groupId, user.Guid);
        if (groupResult.IsFailure) return HandleResult(groupResult.MapTo<ImportStatusDto>());
        var group = groupResult.Value;

        await unitOfWork.BeginTransactionAsync();
        var importResult = await importService.ImportFileAsync(request.File, group!.OriginalId, user.Id);
        if (importResult.IsSuccess) await unitOfWork.CommitTransactionAsync();
        else await unitOfWork.RollbackTransactionAsync();

       return HandleResult(importResult);
    }
}