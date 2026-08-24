using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("user_avatars")]
[Index(nameof(Guid))]
[Index(nameof(UserId), IsUnique = true)]
public class UserAvatar : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("user_id")] public int UserId { get; set; }
    [Column("filename_original"), MaxLength(255)] public string FilenameOriginal { get; set; } = "";
    [Column("stored_filename"), MaxLength(80)] public string StoredFilename { get; set; } = "";
    [Column("file_hash"), MaxLength(64)] public string FileHash { get; set; } = "";
    [Column("mime_type"), MaxLength(100)] public string MimeType { get; set; } = "";
    [Column("size_bytes")] public long SizeBytes { get; set; }
    [Column("content", TypeName = "bytea")] public byte[] Content { get; set; } = [];
    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}
