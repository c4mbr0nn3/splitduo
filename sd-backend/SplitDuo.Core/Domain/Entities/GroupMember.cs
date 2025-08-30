using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("group_members")]
public class GroupMember : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("group_id")] public int GroupId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("role_id")] public int RoleId { get; set; } = (int)GroupRole.Member;

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; }
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; }

    [NotMapped]
    public GroupRole Role
    {
        get => (GroupRole)RoleId;
        set => RoleId = (int)value;
    }
}

public enum GroupRole
{
    Admin = 1,
    Member = 2
}