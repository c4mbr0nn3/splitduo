namespace SplitDuo.Api.Features.ImportExport.Dto;

public class ExportRequestDto
{
    public string Format { get; set; } = "csv";
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool IncludeSettlements { get; set; } = true;
}