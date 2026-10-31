using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("users")]
[Index(nameof(Guid), IsUnique = true)]
public class User : AuditableAndSoftDeletableEntity
{
    private string _email = "";

    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();

    [Column("email"), MaxLength(255)]
    public string Email
    {
        get => _email;
        set => _email = EmailNormalizer.Normalize(value);
    }

    [Column("password_hash"), MaxLength(255)]
    public string PasswordHash { get; set; } = "";

    [Column("first_name"), MaxLength(100)] public string FirstName { get; set; } = "";
    [Column("last_name"), MaxLength(100)] public string LastName { get; set; } = "";
    [Column("global_role_id")] public int GlobalRoleId { get; set; } = (int)GlobalRole.BaseUser;

    // 2FA Properties
    [Column("two_factor_enabled")] public bool TwoFactorEnabled { get; set; } = false;
    [Column("totp_secret")] public string? TotpSecret { get; set; }
    [Column("backup_codes")] public string? BackupCodes { get; set; } // JSON array of hashed backup codes
    [Column("last_used_totp_time_step")] public long? LastUsedTotpTimeStep { get; set; }

    // Account Lockout
    [Column("failed_login_attempts")] public int FailedLoginAttempts { get; set; } = 0;
    [Column("lockout_end")] public long? LockoutEnd { get; set; }

    // Security Stamp
    [Column("security_stamp"), MaxLength(64)] public string SecurityStamp { get; set; } = Guid.CreateVersion7().ToString();

    // Pending email change: set by an admin, consumed only by a token sent to the
    // new address. The token is stored hashed; the raw value never touches the DB.
    [Column("pending_email"), MaxLength(255)] public string? PendingEmail { get; set; }
    [Column("pending_email_token_hash"), MaxLength(255)] public string? PendingEmailTokenHash { get; set; }
    [Column("pending_email_expires_at")] public long? PendingEmailExpiresAt { get; set; }

    public UserSettings Settings { get; set; } = new();

    [NotMapped]
    public GlobalRole GlobalRole
    {
        get => (GlobalRole)GlobalRoleId;
        set => GlobalRoleId = (int)value;
    }
}