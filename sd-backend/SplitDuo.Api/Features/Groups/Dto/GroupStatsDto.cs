using SplitDuo.Api.Features.Expenses.Dto;

namespace SplitDuo.Api.Features.Groups.Dto;

public class CategoryStatDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class MonthlyStatDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class GroupStatsDto
{
    /// <summary>Number of non-deleted expense records in the group (count, not currency).</summary>
    public int ExpenseCount { get; set; }
    /// <summary>Sum of all non-deleted expense amounts in the group (currency).</summary>
    public decimal TotalAmount { get; set; }
    public List<BalanceDto> Balances { get; set; } = [];
    public List<CategoryStatDto> CategoryBreakdown { get; set; } = [];
    public List<MonthlyStatDto> MonthlyBreakdown { get; set; } = [];
}