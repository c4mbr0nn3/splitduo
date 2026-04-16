using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SplitDuo.Core.Domain.Entities;

[Table("ai_call_logs")]
[Index(nameof(UserId))]
[Index(nameof(RequestedAt))]
public class AiCallLog
{
    [Column("id"), Key] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("requested_at")] public long RequestedAt { get; set; }
    [Column("responded_at")] public long? RespondedAt { get; set; }
    [Column("input_tokens")] public int? InputTokens { get; set; }
    [Column("output_tokens")] public int? OutputTokens { get; set; }
    [Column("total_tokens")] public int? TotalTokens { get; set; }
    [Column("model"), MaxLength(255)] public string Model { get; set; } = "";
    [Column("success")] public bool Success { get; set; }
    [Column("error_message")] public string? ErrorMessage { get; set; }

    [ForeignKey(nameof(UserId))] public virtual User User { get; set; } = null!;
}