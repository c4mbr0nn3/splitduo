using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Core.Caching;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/expenses")]
[Authorize]
public class ExpensesController(
    IExpensesService expensesService,
    IUnitOfWork unitOfWork,
    ICacheInvalidator cacheInvalidator,
    ILogger<ExpensesController> logger) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginatedResponseDto<ExpenseDto>>> GetGroupExpenses(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? category = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? search = null)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandlePaginatedResult(NotAuthenticated<PaginatedResponseDto<ExpenseDto>>());

        var filters = new ExpenseFilterOptions(startDate, endDate, category, userId, search);
        var result = await expensesService.GetGroupExpensesAsync(groupId, currentUserId.Value, page, limit, filters);

        return HandlePaginatedResult(result, "Expenses retrieved successfully");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> CreateExpense(string groupId,
        [FromBody] CreateExpenseRequestDto request)
    {
        logger.LogInformation("Creating expense: {ExpenseTitle} in group: {GroupId}", request.Title, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<ExpenseDto>());

        var result = await expensesService.CreateExpenseAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Expense created successfully");
    }

    [HttpGet("{expenseId}")]
    [ProducesResponseType(typeof(ApiResponseDto<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> GetExpense(string groupId, string expenseId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<ExpenseDto>());

        var result = await expensesService.GetExpenseAsync(groupId, expenseId, currentUserId.Value);
        return HandleResult(result, "Expense retrieved successfully");
    }

    [HttpPut("{expenseId}")]
    [ProducesResponseType(typeof(ApiResponseDto<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> UpdateExpense(string groupId, string expenseId,
        [FromBody] UpdateExpenseRequestDto request)
    {
        logger.LogInformation("Updating expense: {ExpenseId} in group: {GroupId}", expenseId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<ExpenseDto>());

        var result = await expensesService.UpdateExpenseAsync(groupId, expenseId, currentUserId.Value, request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Expense updated successfully");
    }

    [HttpDelete("{expenseId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteExpense(string groupId, string expenseId)
    {
        logger.LogWarning("Deleting expense: {ExpenseId} in group: {GroupId}", expenseId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await expensesService.DeleteExpenseAsync(groupId, expenseId, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            await cacheInvalidator.InvalidateGroupAsync(groupId);
        }

        return HandleResult(result, "Expense deleted successfully");
    }
}