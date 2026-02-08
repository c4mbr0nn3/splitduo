using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("invitation_tokens")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(Email), nameof(GroupId))]
[Index(nameof(Email))]
[Index(nameof(GroupId))]
public class InvitationToken : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("email"), MaxLength(255)] public string Email { get; set; } = "";
    [Column("group_id")] public int GroupId { get; set; }
    [Column("invited_by_user_id")] public int InvitedByUserId { get; set; }
    [Column("token_hash"), MaxLength(255)] public string TokenHash { get; set; } = "";
    [Column("expires_at")] public long ExpiresAt { get; set; }
    [Column("accepted_at")] public long? AcceptedAt { get; set; }
    [Column("revoked_at")] public long? RevokedAt { get; set; }

    [ForeignKey(nameof(GroupId))] public virtual Group Group { get; set; } = null!;
    [ForeignKey(nameof(InvitedByUserId))] public virtual User InvitedByUser { get; set; } = null!;

    [NotMapped] public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;
    [NotMapped] public bool IsAccepted => AcceptedAt.HasValue;
    [NotMapped] public bool IsRevoked => RevokedAt.HasValue;
    [NotMapped] public bool IsPending => !IsExpired && !IsAccepted && !IsRevoked;
}