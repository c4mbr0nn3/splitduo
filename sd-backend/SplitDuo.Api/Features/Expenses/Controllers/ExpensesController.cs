using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Expenses.Controllers;

[ApiController]
[Route("api/v1/groups/{groupId}/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponseDto<ExpenseDto>>> GetGroupExpenses(
        string groupId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] string? category = null,
        [FromQuery] string? userId = null)
    {
        // TODO: Implement get group expenses logic
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> CreateExpense(string groupId, [FromBody] CreateExpenseRequestDto request)
    {
        // TODO: Implement create expense logic
        throw new NotImplementedException();
    }

    [HttpGet("{expenseId}")]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> GetExpense(string groupId, string expenseId)
    {
        // TODO: Implement get expense details logic
        throw new NotImplementedException();
    }

    [HttpPut("{expenseId}")]
    public async Task<ActionResult<ApiResponseDto<ExpenseDto>>> UpdateExpense(string groupId, string expenseId, [FromBody] UpdateExpenseRequestDto request)
    {
        // TODO: Implement update expense logic
        throw new NotImplementedException();
    }

    [HttpDelete("{expenseId}")]
    public async Task<ActionResult<ApiResponseDto<object>>> DeleteExpense(string groupId, string expenseId)
    {
        // TODO: Implement delete expense logic
        throw new NotImplementedException();
    }
}