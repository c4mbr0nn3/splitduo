namespace SplitDuo.Api.Features.Expenses.Dto;

public record ExpenseFilterOptions(
    string? StartDate = null,
    string? EndDate = null,
    string? Category = null,
    string? UserId = null,
    string? Search = null);