using System.ComponentModel.DataAnnotations.Schema;

namespace SplitDuo.Core.Entities.Abstract;

public interface IAuditableEntity
{
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}