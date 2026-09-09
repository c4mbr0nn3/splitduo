using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_instance_splits")]
[Index(nameof(UserId))]
[Index(nameof(InstanceId))]
public class RecurringExpenseInstanceSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("instance_id")] public int InstanceId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(InstanceId))] public virtual RecurringExpenseInstance Instance { get; set; } = null!;
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}