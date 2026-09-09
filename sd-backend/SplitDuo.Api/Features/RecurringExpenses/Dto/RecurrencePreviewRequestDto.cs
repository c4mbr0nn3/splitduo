using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class RecurrencePreviewRequestDto
{
    [Required] public int RecurrenceMode { get; set; }
    [Required] public int Weekdays { get; set; }
    public int? DayOfMonth { get; set; }
    public int? Interval { get; set; }
    [Required] public string AnchorDate { get; set; } = "";
    public string? EndDate { get; set; }
}