using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Domain.Base;

namespace SplitDuo.Core.Domain.Entities;

[Table("expense_attachments")]
[Index(nameof(Guid))]
[Index(nameof(ExpenseId), nameof(CreatedAt))]
[Index(nameof(ExpenseId), nameof(FileHash), IsUnique = true)]
public class ExpenseAttachment : AuditableEntity
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("guid")] public Guid Guid { get; set; } = Guid.CreateVersion7();
    [Column("expense_id")] public int ExpenseId { get; set; }
    [Column("filename_original"), MaxLength(255)] public string FilenameOriginal { get; set; } = "";
    [Column("stored_filename"), MaxLength(80)] public string StoredFilename { get; set; } = "";
    [Column("file_hash"), MaxLength(64)] public string FileHash { get; set; } = "";
    [Column("mime_type"), MaxLength(100)] public string MimeType { get; set; } = "";
    [Column("size_bytes")] public long SizeBytes { get; set; }
    [Column("content", TypeName = "bytea")] public byte[] Content { get; set; } = [];
    [ForeignKey(nameof(ExpenseId))] public virtual Expense Expense { get; set; } = null!;
}
