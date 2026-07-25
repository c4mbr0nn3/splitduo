using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Core.Domain.Entities;

[Table("users")]
[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Guid))]
[Index(nameof(DeletedAt))]
public class User : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("email"), MaxLength(255)] public string Email { get; set; } = "";

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

    public UserSettings Settings { get; set; } = new();

    [NotMapped]
    public GlobalRole GlobalRole
    {
        get => (GlobalRole)GlobalRoleId;
        set => GlobalRoleId = (int)value;
    }
}