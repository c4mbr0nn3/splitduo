using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Entities.Abstract;

namespace SplitDuo.Core.Entities;

[Table("imports")]
public class Import : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("filename")] public string FileName { get; set; } = "";
    [Column("import_date")] public DateOnly ImportDate { get; set; }
    [Column("records_count")] public int RecordsCount { get; set; }
    [Column("status_id")] public int StatusId { get; set; } = (int)ImportStatus.Pending;
    [Column("error_details")] public string ErrorDetails { get; set; } = "";

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; }
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; }

    [NotMapped]
    public ImportStatus Status
    {
        get => (ImportStatus)StatusId;
        set => StatusId = (int)value;
    }
}

public enum ImportStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3
}