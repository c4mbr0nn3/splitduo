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
    public int TotalExpenses { get; set; }
    public decimal TotalAmount { get; set; }
    public List<BalanceDto> Balances { get; set; } = [];
    public List<CategoryStatDto> CategoryBreakdown { get; set; } = [];
    public List<MonthlyStatDto> MonthlyBreakdown { get; set; } = [];
}