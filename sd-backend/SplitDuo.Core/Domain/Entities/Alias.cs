using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

/// <summary>
/// IsSingleton marks auto-created singleton aliases so the API can derive
/// IsSingleton in DTOs without counting members. Null = not a singleton.
/// </summary>
[Table("aliases")]
[Index(nameof(Guid))]
[Index(nameof(GroupId))]
[Index(nameof(DeletedAt))]
public class Alias : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("name"), MaxLength(100)] public string Name { get; set; } = "";
    [Column("is_singleton")] public bool? IsSingleton { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; } = null!;
    public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}
