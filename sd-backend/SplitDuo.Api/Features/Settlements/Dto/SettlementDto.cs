using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.Settlements.Dto;

public class SettlementDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public UserBasicInfoDto FromUser { get; set; } = new();
    public string? ToUserId { get; set; }
    public UserBasicInfoDto? ToUser { get; set; }
    public decimal Amount { get; set; }
    public string Date { get; set; } = "";
    public string? Description { get; set; }
    public int ExpenseTypeId { get; set; }
    public int PaymentModeId { get; set; }
    public string? PaidByAliasId { get; set; }
    public string? PaidByAliasName { get; set; }
    public string? ToAliasId { get; set; }
    public string? ToAliasName { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    public SettlementDto()
    {
    }

    public SettlementDto(Expense expense, User fromUser, User? toUser,
        Alias? fromAlias = null, Alias? toAlias = null)
    {
        Id = expense.Guid.ToString();
        GroupId = expense.Group.Guid.ToString();
        FromUserId = fromUser.Guid.ToString();
        FromUser = new UserBasicInfoDto
        {
            Id = fromUser.Guid.ToString(),
            FirstName = fromUser.FirstName,
            LastName = fromUser.LastName
        };
        ToUserId = toUser?.Guid.ToString();
        ToUser = toUser == null ? null : new UserBasicInfoDto
        {
            Id = toUser.Guid.ToString(),
            FirstName = toUser.FirstName,
            LastName = toUser.LastName
        };
        Amount = expense.Amount;
        Date = expense.ExpenseDate.ToString("yyyy-MM-dd");
        Description = expense.Description;
        ExpenseTypeId = expense.ExpenseTypeId;
        PaymentModeId = expense.PaymentModeId;
        PaidByAliasId = fromAlias?.Guid.ToString();
        PaidByAliasName = fromAlias?.Name;
        ToAliasId = toAlias?.Guid.ToString();
        ToAliasName = toAlias?.Name;
        CreatedAt = expense.CreatedAt;
        UpdatedAt = expense.UpdatedAt;
    }
}