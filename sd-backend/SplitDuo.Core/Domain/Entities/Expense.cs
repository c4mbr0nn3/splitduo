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
[Index(nameof(GroupId), nameof(PaymentModeId), nameof(ExpenseDate))]
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
    [Column("payment_mode_id")] public int PaymentModeId { get; set; }
    [Column("import_id")] public int? ImportId { get; set; }
    [Column("paid_by_alias_id")] public int? PaidByAliasId { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; } = null!;
    [ForeignKey(nameof(PaidBy))] public virtual User PaidByUser { get; set; } = null!;
    [ForeignKey(nameof(ImportId))] public virtual Import? Import { get; set; }
    [ForeignKey(nameof(PaidByAliasId))] public virtual Alias? PaidByAlias { get; set; }
    public virtual ICollection<ExpenseSplit> ExpenseSplits { get; set; } = new List<ExpenseSplit>();
    public virtual ICollection<ExpenseAliasSplit> ExpenseAliasSplits { get; set; } = new List<ExpenseAliasSplit>();

    [NotMapped]
    public ExpenseCategory Category
    {
        get => (ExpenseCategory)CategoryId;
        set => CategoryId = (int)value;
    }

    [NotMapped]
    public PaymentMode PaymentMode
    {
        get => (PaymentMode)PaymentModeId;
        set => PaymentModeId = (int)value;
    }
}