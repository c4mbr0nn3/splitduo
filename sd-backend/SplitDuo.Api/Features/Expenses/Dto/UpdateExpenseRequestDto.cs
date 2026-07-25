using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Expenses.Dto;

public class UpdateExpenseRequestDto
{
    [MaxLength(255)] public string? Title { get; set; }
    public string? Description { get; set; }
    [Range(0.01, double.MaxValue)] public decimal? Amount { get; set; }
    public string? PaidByUserId { get; set; }
    public string? ExpenseDate { get; set; }
    public int? CategoryId { get; set; }
    public int? PaymentModeId { get; set; }
    public List<UpdateExpenseSplitDto>? Splits { get; set; }
    public List<CreateExpenseAliasSplitDto>? AliasSplits { get; set; }
}

public class UpdateExpenseSplitDto
{
    [Required] public string UserId { get; set; } = "";
    [Required] public decimal SplitAmount { get; set; }
}