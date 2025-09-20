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
    public async Task<ActionResult<PaginatedResponseDto<ImportStatusDto>>> GetGroupImports(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
            return HandlePaginatedResult(
                Result<PaginatedResponseDto<ImportStatusDto>>.Unauthorized("User not authenticated"));

        var result = await groupsService.GetGroupImportsAsync(groupId, user.Guid, page, limit);
        return HandlePaginatedResult(result, "Imports retrieved successfully");
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

        // Step 1: Create the import entity and temp file (service handles cleanup on failure)
        var insertResult = await importsService.InsertImportJobAsync(request.File, group!.OriginalId, user.Id);
        
        if (insertResult.IsFailure)
        {
            return HandleResult(insertResult);
        }

        // Step 2: Save the import entity to database
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception)
        {
            // If save fails, service will handle cleanup when TriggerImportJobAsync is called or times out
            return HandleResult(Result<ImportStatusDto>.InternalServerError("Failed to save import to database"));
        }

        // Step 3: Now that entity is saved, trigger the background job
        var importGuid = Guid.Parse(insertResult.Value!.Id);
        var triggerResult = await importsService.TriggerImportJobAsync(importGuid);

        if (triggerResult.IsFailure)
        {
            return HandleResult(triggerResult);
        }

        return HandleResult(Result<ImportStatusDto>.Success(triggerResult.Value!), "Import job started successfully");
    }
}