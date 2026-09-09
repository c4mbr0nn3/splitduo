using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class UpdateRecurringExpenseTemplateDto
{
    [Required] [MaxLength(255)] public string Title { get; set; } = "";
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public int CategoryId { get; set; }
    public int PaymentModeId { get; set; }
    [Required] public string PaidByUserId { get; set; } = "";
    public string? PaidByAliasId { get; set; }
    [Required] public int RecurrenceMode { get; set; }
    [Required] public int Weekdays { get; set; }
    public int? DayOfMonth { get; set; }
    public int? Interval { get; set; }
    [Required] public string AnchorDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool RequiresApproval { get; set; }
    public List<CreateRecurringExpenseSplitDto> Splits { get; set; } = [];
    public List<CreateRecurringExpenseAliasSplitDto>? AliasSplits { get; set; }
}