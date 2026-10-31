using Microsoft.Extensions.Logging;
using SplitDuo.Core.Common;

namespace SplitDuo.Core.Services;

public class NoopPushNotificationService(ILogger<NoopPushNotificationService> logger) : IPushNotificationService
{
    private static int _warned;

    public Task<Result> SendAsync(int userId, PushPayload payload, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _warned, 1) == 0)
            logger.LogInformation("Web push is disabled (VAPID keys not configured); push notifications are skipped");

        return Task.FromResult(Result.Success());
    }
}