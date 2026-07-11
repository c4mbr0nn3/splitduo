using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("two_factor_tokens")]
[Index(nameof(UserId))]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(ExpiresAt))]
[Index(nameof(TokenType))]
public class TwoFactorToken : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("token_hash"), MaxLength(255)] public string TokenHash { get; set; } = "";

    [Column("token_type"), MaxLength(50)]
    public string TokenType { get; set; } = ""; // "email_verification", "login_code"

    [Column("purpose"), MaxLength(100)]
    public string Purpose { get; set; } = ""; // "2fa_setup", "2fa_login", "account_verification"

    [Column("expires_at")] public long ExpiresAt { get; set; }
    [Column("used_at")] public long? UsedAt { get; set; }
    [Column("attempts")] public int Attempts { get; set; } = 0;
    [Column("max_attempts")] public int MaxAttempts { get; set; } = 3;

    [Column("client_info"), MaxLength(255)]
    public string ClientInfo { get; set; } = "";

    // Navigation properties
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;

    // Computed properties
    [NotMapped] public bool IsUsed => UsedAt.HasValue;
}