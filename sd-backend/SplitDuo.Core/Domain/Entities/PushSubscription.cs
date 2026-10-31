using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("push_subscriptions")]
[Index(nameof(Endpoint), IsUnique = true)]
[Index(nameof(UserId))]
public class PushSubscription : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("user_id")] public int UserId { get; set; }
    [Column("endpoint"), MaxLength(500)] public string Endpoint { get; set; } = string.Empty;
    [Column("p256dh"), MaxLength(255)] public string P256dh { get; set; } = string.Empty;
    [Column("auth"), MaxLength(255)] public string Auth { get; set; } = string.Empty;
    [Column("user_agent"), MaxLength(500)] public string? UserAgent { get; set; }
    [Column("last_used_at")] public long? LastUsedAt { get; set; }
    [Column("failure_count")] public int FailureCount { get; set; }

    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}