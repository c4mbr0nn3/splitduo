namespace SplitDuo.Core.Caching;

public interface ICacheInvalidator
{
    Task InvalidateGroupAsync(string groupGuid, CancellationToken ct = default);
    Task InvalidateUserAsync(Guid userGuid, CancellationToken ct = default);
}
