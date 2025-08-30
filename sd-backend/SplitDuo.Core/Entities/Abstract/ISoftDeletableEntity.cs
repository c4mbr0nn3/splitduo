using System.ComponentModel.DataAnnotations.Schema;

namespace SplitDuo.Core.Entities.Abstract;

public interface ISoftDeletableEntity
{
    [Column("deleted_at")] public long? DeletedAt { get; set; }
}