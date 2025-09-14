using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Imports.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Imports;

namespace SplitDuo.Api.Features.Imports.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/imports")]
[Authorize]
public class ImportsController(
    IUnitOfWork unitOfWork,
    IGroupsService groupsService,
    IImportServiceFactory importServiceFactory) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<PaginatedResponseDto<ImportStatusDto>>>> GetGroupImports(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return HandleResult(Result<PaginatedResponseDto<ImportStatusDto>>.Unauthorized("User not authenticated"));

        var result = await groupsService.GetGroupImportsAsync(groupId, user.Guid, page, limit);
        return HandleResult(result, "Imports retrieved successfully");
    }

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
        IImportsService importsService;
        try
        {
            importsService = importServiceFactory.GetImportService(importType);
        }
        catch (NotSupportedException ex)
        {
            return HandleResult(Result<ImportStatusDto>.BadRequest(ex.Message));
        }

        // Service handles entity creation and background job scheduling
        var importResult = await importsService.StartImportAsync(request.File, group!.OriginalId, user.Id);

        // Controller only handles SaveChanges
        if (importResult.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
        }

        return HandleResult(importResult, "Import job started successfully");
    }
}