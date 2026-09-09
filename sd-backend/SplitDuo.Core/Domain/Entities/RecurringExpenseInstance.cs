using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_instances")]
[Index(nameof(Guid))]
[Index(nameof(TemplateId), nameof(PeriodDate), IsUnique = true)]
[Index(nameof(TemplateId), nameof(StatusId))]
[Index(nameof(DeletedAt))]
public class RecurringExpenseInstance : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("template_id")] public int TemplateId { get; set; }
    [Column("period_date")] public DateOnly PeriodDate { get; set; }
    [Column("status_id")] public int StatusId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("approved_by_id")] public int? ApprovedById { get; set; }
    [Column("approved_at")] public long? ApprovedAt { get; set; }
    [Column("expense_id")] public int? ExpenseId { get; set; }

    [ForeignKey(nameof(TemplateId))] public virtual RecurringExpenseTemplate Template { get; set; } = null!;
    [ForeignKey(nameof(ApprovedById))] public virtual User? ApprovedBy { get; set; }
    [ForeignKey(nameof(ExpenseId))] public virtual Expense? Expense { get; set; }

    [NotMapped]
    public RecurringExpenseInstanceStatus Status
    {
        get => (RecurringExpenseInstanceStatus)StatusId;
        set => StatusId = (int)value;
    }
}