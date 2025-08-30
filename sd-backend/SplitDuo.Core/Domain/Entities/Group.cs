using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("groups")]
[Index(nameof(Guid))]
[Index(nameof(CreatedBy))]
[Index(nameof(DeletedAt))]
public class Group : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("name"), MaxLength(200)] public string Name { get; set; } = "";

    [Column("description"), MaxLength(255)]
    public string? Description { get; set; }

    [Column("created_by")] public int CreatedBy { get; set; }

    [ForeignKey(nameof(CreatedBy))] public virtual User? CreatedByUser { get; set; }
}