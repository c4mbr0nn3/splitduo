using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SplitDuo.Core.Domain.Entities;

[Table("notifications")]
[Index(nameof(SentAt))]
[Index(nameof(CreatedAt))]
[Index(nameof(CreatedAt), nameof(SentAt))]
[Index(nameof(SentAt), nameof(RetryCount))]
public class Notification
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("to")] public string To { get; set; } = "";
    [Column("subject")] public string Subject { get; set; } = "";
    [Column("body")] public string Body { get; set; } = "";
    [Column("created_at")] public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("sent_at")] public long? SentAt { get; set; }
    [Column("retry_count")] public int RetryCount { get; set; } = 0;
    [Column("error_message")] public string? ErrorMessage { get; set; }
}