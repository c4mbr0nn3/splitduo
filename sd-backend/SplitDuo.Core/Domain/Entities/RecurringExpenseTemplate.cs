using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_templates")]
[Index(nameof(Guid))]
[Index(nameof(GroupId), nameof(IsActive))]
[Index(nameof(DeletedAt))]
public class RecurringExpenseTemplate : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("group_id")] public int GroupId { get; set; }
    [Column("owner_id")] public int OwnerId { get; set; }
    [Column("title")] public string Title { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("category_id")] public int CategoryId { get; set; }
    [Column("payment_mode_id")] public int PaymentModeId { get; set; }
    [Column("paid_by_user_id")] public int PaidByUserId { get; set; }
    [Column("paid_by_alias_id")] public int? PaidByAliasId { get; set; }
    [Column("recurrence_mode_id")] public int RecurrenceModeId { get; set; }
    [Column("weekdays")] public int Weekdays { get; set; }
    [Column("day_of_month")] public int? DayOfMonth { get; set; }
    [Column("interval")] public int? Interval { get; set; }
    [Column("anchor_date")] public DateOnly AnchorDate { get; set; }
    [Column("end_date")] public DateOnly? EndDate { get; set; }
    [Column("requires_approval")] public bool RequiresApproval { get; set; }
    [Column("is_active")] public bool IsActive { get; set; }
    [Column("paused_reason")] public string? PausedReason { get; set; }
    [Column("resume_from")] public DateOnly? ResumeFrom { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; } = null!;
    [ForeignKey(nameof(OwnerId))] public virtual User Owner { get; set; } = null!;
    [ForeignKey(nameof(PaidByUserId))] public virtual User PaidByUser { get; set; } = null!;
    [ForeignKey(nameof(PaidByAliasId))] public virtual Alias? PaidByAlias { get; set; }
    public virtual ICollection<RecurringExpenseTemplateSplit> Splits { get; set; } = new List<RecurringExpenseTemplateSplit>();
    public virtual ICollection<RecurringExpenseTemplateAliasSplit> AliasSplits { get; set; } = new List<RecurringExpenseTemplateAliasSplit>();

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

    [NotMapped]
    public RecurrenceMode RecurrenceMode
    {
        get => (RecurrenceMode)RecurrenceModeId;
        set => RecurrenceModeId = (int)value;
    }
}