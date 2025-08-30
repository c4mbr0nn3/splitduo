using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Expenses.Dto;

public class BalanceDto
{
    public string UserId { get; set; } = "";
    public UserBasicInfoDto User { get; set; } = new();
    public decimal Balance { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
}

public class BalanceSummaryDto
{
    public string GroupId { get; set; } = "";
    public List<BalanceDto> Balances { get; set; } = new();
    public List<BalanceSuggestionDto> Suggestions { get; set; } = new();
}

public class BalanceSuggestionDto
{
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}