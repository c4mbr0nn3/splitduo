using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Expenses.Dto;

public class ExpenseDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string PaidByUserId { get; set; } = "";
    public UserBasicInfoDto PaidByUser { get; set; } = new();
    public string ExpenseDate { get; set; } = "";
    public string Category { get; set; } = "";
    public string PaymentMode { get; set; } = "";
    public List<ExpenseSplitDto> Splits { get; set; } = new();
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}

public class ExpenseSplitDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public UserBasicInfoDto User { get; set; } = new();
    public decimal SplitAmount { get; set; }
    public decimal? SplitPercentage { get; set; }
}