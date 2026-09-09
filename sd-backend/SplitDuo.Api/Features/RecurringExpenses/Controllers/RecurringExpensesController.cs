using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.RecurringExpenses.Dto;
using SplitDuo.Api.Features.RecurringExpenses.Services;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.RecurringExpenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/recurring-expenses")]
[Authorize]
public class RecurringExpensesController(
    IRecurringExpensesService recurringExpensesService,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator,
    ILogger<RecurringExpensesController> logger) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<RecurringExpenseTemplateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<List<RecurringExpenseTemplateDto>>>> GetTemplates(
        string groupId,
        [FromQuery] bool includeInactive = false)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<RecurringExpenseTemplateDto>>());

        var result = await recurringExpensesService.GetTemplatesAsync(groupId, currentUserId.Value, includeInactive);
        return HandleResult(result, "Recurring expense templates retrieved successfully");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<RecurringExpenseTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<RecurringExpenseTemplateDto>>> CreateTemplate(
        string groupId, [FromBody] CreateRecurringExpenseTemplateDto request)
    {
        logger.LogInformation("Creating recurring expense template: {Title} in group: {GroupId}", request.Title, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurringExpenseTemplateDto>());

        var result = await recurringExpensesService.CreateTemplateAsync(groupId, request, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense template created successfully");
    }

    [HttpGet("{templateId}")]
    [ProducesResponseType(typeof(ApiResponseDto<RecurringExpenseTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<RecurringExpenseTemplateDto>>> GetTemplate(
        string groupId, string templateId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurringExpenseTemplateDto>());

        var result = await recurringExpensesService.GetTemplateAsync(groupId, templateId, currentUserId.Value);
        return HandleResult(result, "Recurring expense template retrieved successfully");
    }

    [HttpPut("{templateId}")]
    [ProducesResponseType(typeof(ApiResponseDto<RecurringExpenseTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<RecurringExpenseTemplateDto>>> UpdateTemplate(
        string groupId, string templateId, [FromBody] UpdateRecurringExpenseTemplateDto request)
    {
        logger.LogInformation("Updating recurring expense template: {TemplateId} in group: {GroupId}", templateId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurringExpenseTemplateDto>());

        var result = await recurringExpensesService.UpdateTemplateAsync(groupId, templateId, request, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense template updated successfully");
    }

    [HttpDelete("{templateId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTemplate(string groupId, string templateId)
    {
        logger.LogWarning("Deleting recurring expense template: {TemplateId} in group: {GroupId}", templateId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await recurringExpensesService.DeleteTemplateAsync(groupId, templateId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense template deleted successfully");
    }

    [HttpPost("{templateId}/toggle-active")]
    [ProducesResponseType(typeof(ApiResponseDto<RecurringExpenseTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<RecurringExpenseTemplateDto>>> ToggleActive(
        string groupId, string templateId, [FromBody] ToggleActiveRequestDto request)
    {
        logger.LogInformation("Toggling recurring expense template: {TemplateId} in group: {GroupId}", templateId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurringExpenseTemplateDto>());

        var result = await recurringExpensesService.ToggleActiveAsync(groupId, templateId, request, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense template updated successfully");
    }

    [HttpPost("{templateId}/resume")]
    [ProducesResponseType(typeof(ApiResponseDto<RecurringExpenseTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<RecurringExpenseTemplateDto>>> ResumeTemplate(
        string groupId, string templateId, [FromBody] ResumeRecurringExpenseTemplateDto request)
    {
        logger.LogInformation(
            "Resuming recurring expense template: {TemplateId} in group: {GroupId} with strategy: {Strategy}",
            templateId, groupId, request.Strategy);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurringExpenseTemplateDto>());

        var result = await recurringExpensesService.ResumeAsync(groupId, templateId, request, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense template resumed successfully");
    }

    // Literal segments ("instances", "preview") take precedence over the
    // {templateId} parameter route — no collision with GET/POST {templateId}.

    [HttpGet("instances")]
    [ProducesResponseType(typeof(PaginatedResponseDto<RecurringExpenseInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedResponseDto<RecurringExpenseInstanceDto>>> GetInstances(
        string groupId,
        [FromQuery] string? templateId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(NotAuthenticated<PaginatedResponseDto<RecurringExpenseInstanceDto>>());

        var result = await recurringExpensesService.GetInstancesAsync(
            groupId, templateId, status, page, limit, currentUserId.Value);

        return HandlePaginatedResult(result, "Recurring expense instances retrieved successfully");
    }

    [HttpGet("{templateId}/instances")]
    [ProducesResponseType(typeof(PaginatedResponseDto<RecurringExpenseInstanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedResponseDto<RecurringExpenseInstanceDto>>> GetTemplateInstances(
        string groupId, string templateId,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(NotAuthenticated<PaginatedResponseDto<RecurringExpenseInstanceDto>>());

        var result = await recurringExpensesService.GetInstancesAsync(
            groupId, templateId, status, page, limit, currentUserId.Value);

        return HandlePaginatedResult(result, "Recurring expense instances retrieved successfully");
    }

    [HttpPost("instances/{instanceId}/approve")]
    [ProducesResponseType(typeof(ApiResponseDto<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> ApproveInstance(
        string groupId, string instanceId, [FromBody] ApproveRecurringExpenseInstanceDto? request)
    {
        logger.LogInformation("Approving recurring expense instance: {InstanceId} in group: {GroupId}", instanceId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<ExpenseDto>());

        var result = await recurringExpensesService.ApproveInstanceAsync(groupId, instanceId, request, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Recurring expense instance approved successfully");
    }

    [HttpPost("instances/{instanceId}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RejectInstance(string groupId, string instanceId)
    {
        logger.LogInformation("Rejecting recurring expense instance: {InstanceId} in group: {GroupId}", instanceId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await recurringExpensesService.RejectInstanceAsync(groupId, instanceId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Recurring expense instance rejected successfully");
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponseDto<RecurrencePreviewResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponseDto<RecurrencePreviewResponseDto>>> PreviewOccurrences(
        string groupId, [FromBody] RecurrencePreviewRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<RecurrencePreviewResponseDto>());

        var result = await recurringExpensesService.PreviewOccurrencesAsync(groupId, request, currentUserId.Value);
        return HandleResult(result, "Recurrence preview generated successfully");
    }
}