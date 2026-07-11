using System.ComponentModel.DataAnnotations.Schema;
using SplitDuo.Core.Domain.Interfaces;

namespace SplitDuo.Core.Domain.Base;

public class AuditableAndSoftDeletableEntity : IAuditableEntity, ISoftDeletableEntity
{
    // Intentionally uses DateTimeOffset.UtcNow directly (not TimeProvider): these initializers are
    // a fallback for entities constructed outside the SaveChanges path. AuditSaveChangesInterceptor
    // overwrites both values on every save using an injected TimeProvider, so these only matter
    // when an entity is created but never persisted through the interceptor.
    [Column("created_at")] public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("updated_at")] public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [Column("deleted_at")] public long? DeletedAt { get; set; }
}