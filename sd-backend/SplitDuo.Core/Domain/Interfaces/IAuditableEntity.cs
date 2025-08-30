namespace SplitDuo.Core.Domain.Interfaces;

public interface IAuditableEntity
{
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}