using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Entities.Abstract;

namespace SplitDuo.Core.Entities;

[Table("expenses")]
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

    [NotMapped]
    public ExpenseCategory Category
    {
        get => (ExpenseCategory)CategoryId;
        set => CategoryId = (int)value;
    }
}

public enum ExpenseCategory
{
    Other = 1,
    Food = 2,
    Transportation = 3,
    Utilities = 4,
    Entertainment = 5,
    Health = 6,
    Education = 7,
    Travel = 8,
    Shopping = 9,
    Housing = 10
}