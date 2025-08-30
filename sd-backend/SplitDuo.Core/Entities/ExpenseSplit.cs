using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Entities.Abstract;

namespace SplitDuo.Core.Entities;

[Table("expense_splits")]
public class ExpenseSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("expense_id")] public int ExpenseId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(ExpenseId))] public virtual Expense Expense { get; set; }
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; }
}