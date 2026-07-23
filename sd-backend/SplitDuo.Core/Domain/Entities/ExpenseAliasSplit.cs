using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("expense_alias_splits")]
[Index(nameof(AliasId))]
[Index(nameof(ExpenseId))]
public class ExpenseAliasSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("expense_id")] public int ExpenseId { get; set; }
    [Column("alias_id")] public int AliasId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(ExpenseId))] public virtual Expense Expense { get; set; } = null!;
    [ForeignKey(nameof(AliasId))] public virtual Alias Alias { get; set; } = null!;
}
