using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("expenses")]
[Index(nameof(Guid))]
[Index(nameof(GroupId), nameof(ExpenseDate))]
[Index(nameof(PaidBy), nameof(ExpenseDate))]
[Index(nameof(GroupId), nameof(CategoryId), nameof(ExpenseDate))]
[Index(nameof(ExpenseDate))]
[Index(nameof(DeletedAt))]
public class Expense : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("title")] public string Title { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("paid_by")] public int PaidBy { get; set; }
    [Column("expense_date")] public DateOnly ExpenseDate { get; set; }
    [Column("category_id")] public int CategoryId { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; }
    [ForeignKey(nameof(PaidBy))] public virtual User PaidByUser { get; set; }
    public virtual ICollection<ExpenseSplit> ExpenseSplits { get; set; } = new List<ExpenseSplit>();

    [NotMapped]
    public ExpenseCategory Category
    {
        get => (ExpenseCategory)CategoryId;
        set => CategoryId = (int)value;
    }
}