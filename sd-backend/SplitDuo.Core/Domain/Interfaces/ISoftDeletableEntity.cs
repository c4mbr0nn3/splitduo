using System.ComponentModel.DataAnnotations.Schema;

namespace SplitDuo.Core.Domain.Interfaces;

public interface ISoftDeletableEntity
{
    [Column("deleted_at")] public long? DeletedAt { get; set; }
}