using System.ComponentModel.DataAnnotations.Schema;

namespace SplitDuo.Core.Entities.Abstract;

public class AuditableEntity : IAuditableEntity
{
    [Column("created_at")] public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("updated_at")] public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}