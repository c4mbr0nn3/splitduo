using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_template_splits")]
[Index(nameof(UserId))]
[Index(nameof(TemplateId))]
public class RecurringExpenseTemplateSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("template_id")] public int TemplateId { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(TemplateId))] public virtual RecurringExpenseTemplate Template { get; set; } = null!;
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}