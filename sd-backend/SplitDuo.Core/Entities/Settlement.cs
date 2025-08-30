using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Entities.Abstract;

namespace SplitDuo.Core.Entities;

[Table("settlements")]
public class Settlement : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("from_user_id")] public int FromUserId { get; set; }
    [Column("to_user_id")] public int ToUserId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("settlement_date")] public DateOnly SettlementDate { get; set; }
    [Column("description")] public string? Description { get; set; }
    
    [ForeignKey(nameof(GroupId))] public Group Group { get; set; }
    [ForeignKey(nameof(FromUserId))] public User FromUser { get; set; }
    [ForeignKey(nameof(ToUserId))] public User ToUser { get; set; }
}