using SplitDuo.Api.Features.Expenses.Dto;

namespace SplitDuo.Api.Features.Groups.Dto;

public class GroupStatsDto
{
    public int TotalExpenses { get; set; }
    public decimal TotalAmount { get; set; }
    public List<BalanceDto> Balances { get; set; } = [];
}