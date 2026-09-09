using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class RecurringExpenseTemplateDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int CategoryId { get; set; }
    public int PaymentModeId { get; set; }
    public string PaidByUserId { get; set; } = "";
    public string? PaidByAliasId { get; set; }
    public int RecurrenceMode { get; set; }
    public int Weekdays { get; set; }
    public int? DayOfMonth { get; set; }
    public int? Interval { get; set; }
    public string AnchorDate { get; set; } = "";
    public string? EndDate { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsActive { get; set; }
    public string? PausedReason { get; set; }
    public int MissedCount { get; set; }
    public List<string> SkippedPeriodDates { get; set; } = [];
    public string? ResumeFrom { get; set; }
    public string? NextOccurrence { get; set; }
    public string Summary { get; set; } = "";
    public List<RecurringExpenseSplitDto> Splits { get; set; } = [];
    public List<RecurringExpenseAliasSplitDto>? AliasSplits { get; set; }
    public int PendingCount { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    // Default constructor
    public RecurringExpenseTemplateDto()
    {
    }

    // Constructor that takes a RecurringExpenseTemplate entity plus computed values.
    // Splits/aliasSplits are the (user, alias) entities to project; computed values
    // (nextOccurrence, summary, pendingCount) are supplied by the caller.
    public RecurringExpenseTemplateDto(
        RecurringExpenseTemplate template,
        string? nextOccurrence,
        string summary,
        int pendingCount,
        List<RecurringExpenseTemplateSplit>? splits = null,
        List<RecurringExpenseTemplateAliasSplit>? aliasSplits = null,
        int missedCount = 0,
        List<string>? skippedPeriodDates = null)
    {
        Id = template.Guid.ToString();
        GroupId = template.Group.Guid.ToString();
        OwnerId = template.Owner.Guid.ToString();
        Title = template.Title;
        Description = template.Description;
        Amount = template.Amount;
        CategoryId = template.CategoryId;
        PaymentModeId = template.PaymentModeId;
        PaidByUserId = template.PaidByUser.Guid.ToString();
        PaidByAliasId = template.PaidByAlias?.Guid.ToString();
        RecurrenceMode = template.RecurrenceModeId;
        Weekdays = template.Weekdays;
        DayOfMonth = template.DayOfMonth;
        Interval = template.Interval;
        AnchorDate = template.AnchorDate.ToString("yyyy-MM-dd");
        EndDate = template.EndDate?.ToString("yyyy-MM-dd");
        RequiresApproval = template.RequiresApproval;
        IsActive = template.IsActive;
        PausedReason = template.PausedReason;
        MissedCount = missedCount;
        SkippedPeriodDates = skippedPeriodDates ?? [];
        ResumeFrom = template.ResumeFrom?.ToString("yyyy-MM-dd");
        NextOccurrence = nextOccurrence;
        Summary = summary;
        PendingCount = pendingCount;
        CreatedAt = template.CreatedAt;
        UpdatedAt = template.UpdatedAt;

        if (splits != null)
        {
            Splits = splits.Select(split => new RecurringExpenseSplitDto
            {
                UserId = split.User.Guid.ToString(),
                SplitAmount = split.SplitAmount
            }).ToList();
        }

        if (aliasSplits != null)
        {
            AliasSplits = aliasSplits.Select(aliasSplit => new RecurringExpenseAliasSplitDto
            {
                AliasId = aliasSplit.Alias.Guid.ToString(),
                SplitAmount = aliasSplit.SplitAmount
            }).ToList();
        }
    }
}

public class RecurringExpenseSplitDto
{
    public string UserId { get; set; } = "";
    public decimal SplitAmount { get; set; }
}

public class RecurringExpenseAliasSplitDto
{
    public string AliasId { get; set; } = "";
    public decimal SplitAmount { get; set; }
}