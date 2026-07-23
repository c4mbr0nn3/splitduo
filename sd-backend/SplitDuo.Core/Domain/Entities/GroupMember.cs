using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("group_members")]
[Index(nameof(UserId))]
[Index(nameof(GroupId))]
[Index(nameof(DeletedAt))]
public class GroupMember : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("group_id")] public int GroupId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("role_id")] public int RoleId { get; set; } = (int)GroupRole.Member;
    [Column("alias_id")] public int? AliasId { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; } = null!;
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
    [ForeignKey(nameof(AliasId))] public virtual Alias? Alias { get; set; }

    [NotMapped]
    public GroupRole Role
    {
        get => (GroupRole)RoleId;
        set => RoleId = (int)value;
    }
}