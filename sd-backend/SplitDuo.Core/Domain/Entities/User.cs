using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("users")]
public class User : AuditableAndSoftDeletableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("email"), MaxLength(255)] public string Email { get; set; } = "";

    [Column("password_hash"), MaxLength(255)]
    public string PasswordHash { get; set; } = "";

    [Column("first_name"), MaxLength(100)] public string FirstName { get; set; } = "";
    [Column("last_name"), MaxLength(100)] public string LastName { get; set; } = "";
}