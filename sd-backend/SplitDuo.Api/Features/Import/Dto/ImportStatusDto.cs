namespace SplitDuo.Api.Features.Import.Dto;

public class ImportStatusDto
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public int ImportStatusId { get; set; }
    public int RecordsImported { get; set; }
    public string ErrorDetails { get; set; } = "";
    public string ImportDate { get; set; } = "";
}