using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Expenses.Dto;

public class CreateExpenseRequestDto
{
    [Required] [MaxLength(255)] public string Title { get; set; } = "";
    public string? Description { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required] public string PaidByUserId { get; set; } = "";
    [Required] public string ExpenseDate { get; set; } = "";
    public int CategoryId { get; set; }
    public int PaymentModeId { get; set; }
    public List<CreateExpenseSplitDto> Splits { get; set; } = [];
}

public class CreateExpenseSplitDto
{
    [Required] public string UserId { get; set; } = "";
    public decimal? SplitAmount { get; set; }
    [Range(0, 100)] public decimal? SplitPercentage { get; set; }
}