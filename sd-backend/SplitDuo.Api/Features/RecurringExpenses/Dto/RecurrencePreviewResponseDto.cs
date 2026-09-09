namespace SplitDuo.Api.Features.RecurringExpenses.Dto;

public class RecurrencePreviewResponseDto
{
    public List<string> NextOccurrences { get; set; } = [];
    public string Summary { get; set; } = "";
}