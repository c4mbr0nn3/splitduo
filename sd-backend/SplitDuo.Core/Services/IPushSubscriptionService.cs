using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Core.Services;

public interface IPushSubscriptionService
{
    Task<Result> UpsertAsync(int userId, string endpoint, string p256dh, string auth, string? userAgent);
    Task<Result> DeleteAsync(int userId, string endpoint);
    Task<Result<IReadOnlyList<PushSubscription>>> GetByUserAsync(int userId);
}