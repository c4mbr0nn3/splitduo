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

public class AliasBalanceDto
{
    public string AliasId { get; set; } = "";
    public string AliasName { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    public List<UserBasicInfoDto> Members { get; set; } = [];
    public bool IsSingleton { get; set; }
}

public class BalanceSummaryDto
{
    public string GroupId { get; set; } = "";
    public List<BalanceDto> Balances { get; set; } = [];
    public List<BalanceSuggestionDto> Suggestions { get; set; } = [];
}

public class AliasBalanceSummaryDto
{
    public string GroupId { get; set; } = "";
    public List<AliasBalanceDto> Balances { get; set; } = [];
    public List<AliasSettlementSuggestionDto> Suggestions { get; set; } = [];
}

public class BalanceSuggestionDto
{
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}

public class AliasSettlementSuggestionDto
{
    public string FromAliasId { get; set; } = "";
    public string ToAliasId { get; set; } = "";
    public string FromAliasName { get; set; } = "";
    public string ToAliasName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}