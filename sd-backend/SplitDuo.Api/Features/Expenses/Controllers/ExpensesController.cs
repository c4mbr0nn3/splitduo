using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/expenses")]
[Authorize]
public class ExpensesController(
    IExpensesService expensesService,
    IUnitOfWork unitOfWork,
    ILogger<ExpensesController> logger) : BaseApiController
{
    [HttpGet]
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
            return HandlePaginatedResult(
                Result<PaginatedResponseDto<ExpenseDto>>.Unauthorized("User not authenticated"));

        var filters = new ExpenseFilterOptions(startDate, endDate, category, userId, search);
        var result = await expensesService.GetGroupExpensesAsync(groupId, currentUserId.Value, page, limit, filters);

        return HandlePaginatedResult(result, "Expenses retrieved successfully");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> CreateExpense(string groupId,
        [FromBody] CreateExpenseRequestDto request)
    {
        logger.LogInformation("Creating expense: {ExpenseTitle} in group: {GroupId}", request.Title, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<ExpenseDto>.Unauthorized("User not authenticated"));

        var result = await expensesService.CreateExpenseAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Expense created successfully");
    }

    [HttpGet("{expenseId}")]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> GetExpense(string groupId, string expenseId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<ExpenseDto>.Unauthorized("User not authenticated"));

        var result = await expensesService.GetExpenseAsync(groupId, expenseId, currentUserId.Value);
        return HandleResult(result, "Expense retrieved successfully");
    }

    [HttpPut("{expenseId}")]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> UpdateExpense(string groupId, string expenseId,
        [FromBody] UpdateExpenseRequestDto request)
    {
        logger.LogInformation("Updating expense: {ExpenseId} in group: {GroupId}", expenseId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<ExpenseDto>.Unauthorized("User not authenticated"));

        var result = await expensesService.UpdateExpenseAsync(groupId, expenseId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Expense updated successfully");
    }

    [HttpDelete("{expenseId}")]
    public async Task<ActionResult> DeleteExpense(string groupId, string expenseId)
    {
        logger.LogWarning("Deleting expense: {ExpenseId} in group: {GroupId}", expenseId, groupId);

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

        var result = await expensesService.DeleteExpenseAsync(groupId, expenseId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Expense deleted successfully");
    }
}