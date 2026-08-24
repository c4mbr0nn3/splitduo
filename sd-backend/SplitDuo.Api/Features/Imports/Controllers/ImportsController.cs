using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Services;
using SplitDuo.Api.Features.Imports.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
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
    IImportServiceFactory importServiceFactory,
    IImportValidatorService validatorService) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<ImportStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(ApiResponseDto<ImportStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> AnalyzeImportFile(
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

        IImportsService importsService;
        try
        {
            importsService = importServiceFactory.GetImportService(importType);
        }
        catch (NotSupportedException ex)
        {
            return HandleResult(Result<ImportStatusDto>.BadRequest(ex.Message));
        }

        var validationResult = await validatorService.IsValidImportAsync(request.File, group!.OriginalId);
        if (validationResult.IsFailure)
            return HandleResult(validationResult);

        var analysisResult = await importsService.AnalyzeFileAsync(request.File);
        if (analysisResult.IsFailure) return HandleResult(analysisResult.MapTo<ImportStatusDto>());

        var createImportResult = await importsService.CreateImportJobAsync(
            request.File,
            group.OriginalId,
            user.Id,
            analysisResult.Value!);
        if (createImportResult.IsFailure) return HandleResult(createImportResult);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception)
        {
            return HandleResult(Result<ImportStatusDto>.InternalServerError("Failed to save import to database"));
        }

        return HandleResult(createImportResult, "File analyzed successfully");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<ImportStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponseDto<ImportStatusDto>>> ImportData(
        string groupId,
        ImportMappingDto request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return HandleResult(Result<ImportStatusDto>.Unauthorized("User not authenticated"));

        if (!Guid.TryParse(request.ImportId, out var importId))
            return HandleResult(Result<ImportStatusDto>.BadRequest("Invalid import ID format"));

        var importResult = await groupsService.GetImportStatusAsync(importId, user.Guid);
        if (importResult.IsFailure) return HandleResult(importResult.MapTo<ImportStatusDto>());
        var import = importResult.Value;

        var groupResult = await groupsService.GetGroupAsync(groupId, user.Guid);
        if (groupResult.IsFailure) return HandleResult(groupResult.MapTo<ImportStatusDto>());

        IImportsService importsService;
        try
        {
            var importType = (ImportType)import!.ImportTypeId;
            importsService = importServiceFactory.GetImportService(importType);
        }
        catch (NotSupportedException ex)
        {
            return HandleResult(Result<ImportStatusDto>.BadRequest(ex.Message));
        }

        var mappingResult = await importsService.UpdateImportMappingsAsync(importId, request);

        if (mappingResult.IsFailure)
        {
            return HandleResult(mappingResult);
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (Exception)
        {
            return HandleResult(Result<ImportStatusDto>.InternalServerError("Failed to save import to database"));
        }

        var triggerResult = await importsService.TriggerImportJobAsync(importId);

        if (triggerResult.IsFailure)
        {
            return HandleResult(triggerResult);
        }

        return HandleResult(Result<ImportStatusDto>.Success(triggerResult.Value!), "Import job started successfully");
    }
}