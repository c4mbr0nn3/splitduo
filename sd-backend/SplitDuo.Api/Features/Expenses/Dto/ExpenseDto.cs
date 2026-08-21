using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Domain.Entities;

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
    public int CategoryId { get; set; }
    public int PaymentModeId { get; set; }
    public List<ExpenseSplitDto> Splits { get; set; } = [];
    public List<ExpenseAliasSplitDto>? AliasSplits { get; set; }
    public int AttachmentCount { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    // Default constructor
    public ExpenseDto()
    {
    }

    // Constructor that takes an Expense entity
    public ExpenseDto(Expense expense, List<ExpenseSplit>? splits = null, List<ExpenseAliasSplit>? aliasSplits = null,
        int? attachmentCount = null)
    {
        AttachmentCount = attachmentCount ?? 0;

        Id = expense.Guid.ToString();
        GroupId = expense.Group.Guid.ToString();
        Title = expense.Title;
        Description = expense.Description;
        Amount = expense.Amount;
        PaidByUserId = expense.PaidByUser.Guid.ToString();
        PaidByUser = new UserBasicInfoDto
        {
            Id = expense.PaidByUser.Guid.ToString(),
            FirstName = expense.PaidByUser.FirstName,
            LastName = expense.PaidByUser.LastName
        };
        ExpenseDate = expense.ExpenseDate.ToString("yyyy-MM-dd");
        CategoryId = expense.CategoryId;
        PaymentModeId = expense.PaymentModeId;
        CreatedAt = expense.CreatedAt;
        UpdatedAt = expense.UpdatedAt;

        // Map splits if provided
        if (splits != null)
        {
            Splits = splits.Select(split => new ExpenseSplitDto
            {
                Id = split.Id.ToString(),
                UserId = split.User.Guid.ToString(),
                User = new UserBasicInfoDto
                {
                    Id = split.User.Guid.ToString(),
                    FirstName = split.User.FirstName,
                    LastName = split.User.LastName
                },
                SplitAmount = split.SplitAmount,
                SplitPercentage = expense.Amount > 0 ? split.SplitAmount / expense.Amount * 100 : null
            }).ToList();
        }

        // Map alias splits if provided
        if (aliasSplits != null)
        {
            AliasSplits = aliasSplits.Select(aliasSplit => new ExpenseAliasSplitDto
            {
                Id = aliasSplit.Id.ToString(),
                AliasId = aliasSplit.Alias.Guid.ToString(),
                AliasName = aliasSplit.Alias.Name,
                SplitAmount = aliasSplit.SplitAmount,
                SplitPercentage = expense.Amount > 0 ? aliasSplit.SplitAmount / expense.Amount * 100 : null
            }).ToList();
        }
    }
}

public class ExpenseSplitDto
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public UserBasicInfoDto User { get; set; } = new();
    public decimal SplitAmount { get; set; }
    public decimal? SplitPercentage { get; set; }
}

public class ExpenseAliasSplitDto
{
    public string Id { get; set; } = "";
    public string AliasId { get; set; } = "";
    public string AliasName { get; set; } = "";
    public decimal SplitAmount { get; set; }
    public decimal? SplitPercentage { get; set; }
}