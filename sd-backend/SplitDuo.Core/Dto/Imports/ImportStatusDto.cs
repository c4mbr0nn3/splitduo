using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Dto.Imports;

public class ImportStatusDto
{
    public string Id { get; set; } = "";
    public string? GroupId { get; set; }
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public int ImportStatusId { get; set; } = (int)ImportStatus.Pending;
    public int ImportTypeId { get; set; }
    public int RecordsCount { get; set; }
    public string ErrorDetails { get; set; } = "";
    public string ImportDate { get; set; } = "";
    public long? StartedAt { get; set; }
    public long? CompletedAt { get; set; }
    public long? Duration { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public string? AnalysisResults { get; set; }
    public string? MappingConfiguration { get; set; }

    public ImportStatusDto()
    {
    }

    public ImportStatusDto(Import import)
    {
        Id = import.Guid.ToString();
        GroupId = import.Group?.Guid.ToString() ?? null;
        FileName = import.FileName;
        FileHash = import.FileHash;
        ImportStatusId = import.StatusId;
        ImportTypeId = import.ImportTypeId;
        RecordsCount = import.RecordsCount;
        ErrorDetails = import.ErrorDetails;
        ImportDate = import.ImportDate.ToString("yyyy-MM-dd");
        StartedAt = import.StartedAt;
        CompletedAt = import.CompletedAt;
        Duration = import.Duration;
        CreatedAt = import.CreatedAt;
        UpdatedAt = import.UpdatedAt;
        AnalysisResults = import.AnalysisResults;
        MappingConfiguration = import.MappingConfiguration;
    }
}