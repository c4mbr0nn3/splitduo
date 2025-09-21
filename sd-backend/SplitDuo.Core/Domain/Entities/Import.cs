using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("imports")]
[Index(nameof(Guid))]
[Index(nameof(UserId), nameof(ImportDate))]
[Index(nameof(GroupId), nameof(ImportDate))]
[Index(nameof(StatusId), nameof(ImportDate))]
[Index(nameof(GroupId), nameof(FileHash), IsUnique = true)]
public class Import : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("filename")] public string FileName { get; set; } = "";
    [Column("file_hash"), MaxLength(64)] public string FileHash { get; set; } = "";
    [Column("import_date")] public DateOnly ImportDate { get; set; }
    [Column("records_count")] public int RecordsCount { get; set; }
    [Column("status_id")] public int StatusId { get; set; } = (int)ImportStatus.Pending;
    [Column("import_type_id")] public int ImportTypeId { get; set; }
    [Column("error_details")] public string ErrorDetails { get; set; } = "";
    [Column("started_at")] public long? StartedAt { get; set; }
    [Column("completed_at")] public long? CompletedAt { get; set; }
    [Column("duration_ms")] public long? Duration { get; set; }

    [Column("analysis_results", TypeName = "jsonb")]
    public string? AnalysisResults { get; set; }

    [Column("mapping_configuration", TypeName = "jsonb")]
    public string? MappingConfiguration { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group? Group { get; set; }
    [ForeignKey(nameof(UserId))] public virtual User? User { get; set; }

    [NotMapped]
    public ImportStatus Status
    {
        get => (ImportStatus)StatusId;
        set => StatusId = (int)value;
    }

    [NotMapped]
    public ImportType ImportType
    {
        get => (ImportType)ImportTypeId;
        set => ImportTypeId = (int)value;
    }

    // Helper methods for accessing JSON data
    public T? GetAnalysisResults<T>() where T : class => string.IsNullOrEmpty(AnalysisResults)
        ? null
        : JsonSerializer.Deserialize<T>(AnalysisResults);

    public void SetAnalysisResults<T>(T data) where T : class => AnalysisResults = JsonSerializer.Serialize(data);

    public T? GetMappingConfiguration<T>() where T : class => string.IsNullOrEmpty(MappingConfiguration)
        ? null
        : JsonSerializer.Deserialize<T>(MappingConfiguration);

    public void SetMappingConfiguration<T>(T data) where T : class =>
        MappingConfiguration = JsonSerializer.Serialize(data);
}