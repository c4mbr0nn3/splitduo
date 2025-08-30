using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("refresh_tokens")]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(UserId), nameof(RevokedAt))]
[Index(nameof(ExpiresAt))]
[Index(nameof(JwtId))]
public class RefreshToken : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("token_hash"), MaxLength(255)] public string TokenHash { get; set; } = "";
    [Column("jwt_id"), MaxLength(255)] public string JwtId { get; set; } = "";
    [Column("expires_at")] public long ExpiresAt { get; set; }
    [Column("revoked_at")] public long? RevokedAt { get; set; }

    [Column("revoked_reason"), MaxLength(255)]
    public string? RevokedReason { get; set; }

    [Column("replaced_by_token"), MaxLength(255)]
    public string? ReplacedByToken { get; set; }

    [Column("client_info"), MaxLength(255)]
    public string ClientInfo { get; set; } = "";

    [NotMapped] public bool IsExpired => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiresAt;
    [NotMapped] public bool IsRevoked => RevokedAt.HasValue;
    [NotMapped] public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation properties
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}