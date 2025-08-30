using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("expense_splits")]
[Index(nameof(UserId))]
[Index(nameof(ExpenseId))]
public class ExpenseSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("expense_id")] public int ExpenseId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(ExpenseId))] public virtual Expense Expense { get; set; }
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; }
}