using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_instance_alias_splits")]
[Index(nameof(AliasId))]
[Index(nameof(InstanceId))]
public class RecurringExpenseInstanceAliasSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("instance_id")] public int InstanceId { get; set; }
    [Column("alias_id")] public int AliasId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(InstanceId))] public virtual RecurringExpenseInstance Instance { get; set; } = null!;
    [ForeignKey(nameof(AliasId))] public virtual Alias Alias { get; set; } = null!;
}