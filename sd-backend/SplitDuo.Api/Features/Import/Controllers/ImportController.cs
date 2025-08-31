using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Import.Dto;
using SplitDuo.Api.Features.Import.Services;
using SplitDuo.Api.Features.Import.Factories;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Import.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/import")]
[Authorize]
public class ImportController(
    IUnitOfWork unitOfWork,
    IGroupsService groupsService,
    IImportServiceFactory importServiceFactory) : BaseApiController
{
    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportData(
        string groupId,
        [FromForm] ImportRequestDto request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return HandleResult(Result<ImportStatusDto>.Unauthorized("User not authenticated"));

        // Validate ImportTypeId
        if (!Enum.IsDefined(typeof(ImportType), request.ImportTypeId))
        {
            return HandleResult(Result<ImportStatusDto>.BadRequest("Invalid import type"));
        }

        var importType = (ImportType)request.ImportTypeId;

        var groupResult = await groupsService.GetGroupAsync(groupId, user.Guid);
        if (groupResult.IsFailure) return HandleResult(groupResult.MapTo<ImportStatusDto>());
        var group = groupResult.Value;

        // Get the appropriate import service
        IImportService importService;
        try
        {
            importService = importServiceFactory.GetImportService(importType);
        }
        catch (NotSupportedException ex)
        {
            return HandleResult(Result<ImportStatusDto>.BadRequest(ex.Message));
        }

        await unitOfWork.BeginTransactionAsync();
        var importResult = await importService.ImportFileAsync(request.File, group!.OriginalId, user.Id);
        if (importResult.IsSuccess) await unitOfWork.CommitTransactionAsync();
        else await unitOfWork.RollbackTransactionAsync();

        return HandleResult(importResult, "Import completed successfully");
    }
}