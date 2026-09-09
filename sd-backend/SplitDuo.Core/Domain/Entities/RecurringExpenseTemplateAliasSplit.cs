using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("recurring_expense_template_alias_splits")]
[Index(nameof(AliasId))]
[Index(nameof(TemplateId))]
public class RecurringExpenseTemplateAliasSplit : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("template_id")] public int TemplateId { get; set; }
    [Column("alias_id")] public int AliasId { get; set; }
    [Column("split_amount")] public decimal SplitAmount { get; set; }

    [ForeignKey(nameof(TemplateId))] public virtual RecurringExpenseTemplate Template { get; set; } = null!;
    [ForeignKey(nameof(AliasId))] public virtual Alias Alias { get; set; } = null!;
}