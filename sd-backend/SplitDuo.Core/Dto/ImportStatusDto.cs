using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Dto;

public class ImportStatusDto
{
    public string FileName { get; set; } = "";
    public int ImportStatusId { get; set; } = (int)ImportStatus.Pending;
    public int RecordsCount { get; set; }
    public string ErrorDetails { get; set; } = "";
    public string ImportDate { get; set; } = "";

    public ImportStatusDto()
    {
    }

    public ImportStatusDto(Import import)
    {
        FileName = import.FileName;
        ImportStatusId = import.StatusId;
        RecordsCount = import.RecordsCount;
        ErrorDetails = import.ErrorDetails;
        ImportDate = import.ImportDate.ToString("yyyy-MM-dd");
    }
}