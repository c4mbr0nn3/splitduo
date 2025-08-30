namespace SplitDuo.Api.Features.Import.Dto;

public class ImportStatusDto
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Status { get; set; } = "";
    public int RecordsImported { get; set; }
    public string ErrorDetails { get; set; } = "";
    public string ImportDate { get; set; } = "";
}