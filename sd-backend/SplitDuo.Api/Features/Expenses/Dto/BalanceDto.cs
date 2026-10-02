using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Expenses.Dto;

public class BalanceDto
{
    public string UserId { get; set; } = "";
    public UserBasicInfoDto User { get; set; } = new();
    public decimal Balance { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    /// <summary>
    /// Settlement-excluded: sum of expense Amount paid by this user (ExpenseTypeId != Settlement).
    /// Like <see cref="TotalOwed"/>, only accumulates for current group members,
    /// so Σ ExpensePaid can be less than GroupStats.TotalAmount if a payer was removed.
    /// </summary>
    public decimal ExpensePaid { get; set; }
    /// <summary>Settlement-excluded: sum of this user's ExpenseSplit.SplitAmount over non-settlement expenses.</summary>
    public decimal ExpenseShare { get; set; }
}

public class AliasBalanceDto
{
    public string AliasId { get; set; } = "";
    public string AliasName { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    /// <summary>
    /// Settlement-excluded: sum of expense Amount attributed to this alias (ExpenseTypeId != Settlement).
    /// Like <see cref="TotalOwed"/>, only accumulates for present aliases,
    /// so Σ ExpensePaid can be less than GroupStats.TotalAmount if an alias was removed.
    /// </summary>
    public decimal ExpensePaid { get; set; }
    /// <summary>Settlement-excluded: sum of ExpenseAliasSplit.SplitAmount for this alias over non-settlement expenses.</summary>
    public decimal ExpenseShare { get; set; }
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