using System.ComponentModel.DataAnnotations.Schema;

namespace SplitDuo.Core.Entities.Abstract;

public class AuditableAndSoftDeletableEntity : IAuditableEntity, ISoftDeletableEntity
{
    [Column("created_at")] public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("updated_at")] public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("deleted_at")] public long? DeletedAt { get; set; }
}