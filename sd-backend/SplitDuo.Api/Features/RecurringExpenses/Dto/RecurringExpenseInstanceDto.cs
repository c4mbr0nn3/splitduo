using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class RecurringExpenseInstanceDto
{
    public string Id { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string TemplateTitle { get; set; } = "";
    public string PeriodDate { get; set; } = "";
    public int Status { get; set; }
    public decimal Amount { get; set; }
    public string? ApprovedBy { get; set; }
    public long? ApprovedAt { get; set; }
    public string? ExpenseId { get; set; }
    public List<RecurringExpenseSplitDto> Splits { get; set; } = [];
    public List<RecurringExpenseAliasSplitDto>? AliasSplits { get; set; }
    public long CreatedAt { get; set; }

    // Default constructor
    public RecurringExpenseInstanceDto()
    {
    }

    // Constructor that takes a RecurringExpenseInstance entity
    public RecurringExpenseInstanceDto(RecurringExpenseInstance instance)
    {
        Id = instance.Guid.ToString();
        TemplateId = instance.Template.Guid.ToString();
        TemplateTitle = instance.Template.Title;
        PeriodDate = instance.PeriodDate.ToString("yyyy-MM-dd");
        Status = instance.StatusId;
        Amount = instance.Amount;
        ApprovedBy = instance.ApprovedBy?.Guid.ToString();
        ApprovedAt = instance.ApprovedAt;
        ExpenseId = instance.Expense?.Guid.ToString();
        CreatedAt = instance.CreatedAt;
    }
}