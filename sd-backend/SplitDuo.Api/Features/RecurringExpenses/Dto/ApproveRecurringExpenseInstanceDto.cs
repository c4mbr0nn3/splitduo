namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class ApproveRecurringExpenseInstanceDto
{
    public decimal? Amount { get; set; }
    public string? ExpenseDate { get; set; }
    public List<RecurringExpenseSplitDto>? Splits { get; set; }
    public List<RecurringExpenseAliasSplitDto>? AliasSplits { get; set; }
}