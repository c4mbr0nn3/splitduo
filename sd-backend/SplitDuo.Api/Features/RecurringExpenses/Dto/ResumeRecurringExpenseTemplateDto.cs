namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class ResumeRecurringExpenseTemplateDto
{
    /// <summary>
    /// "backfill" (generate missed periods) or "skip" (permanently exclude missed periods).
    /// </summary>
    public string Strategy { get; set; } = "";
}