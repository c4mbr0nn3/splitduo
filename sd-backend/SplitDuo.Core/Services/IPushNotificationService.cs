using SplitDuo.Core.Common;

namespace SplitDuo.Core.Services;

public sealed record PushPayload(string Title, string BodyKey, object?[] BodyArgs, string Url, string Tag);

public interface IPushNotificationService
{
    Task<Result> SendAsync(int userId, PushPayload payload, CancellationToken cancellationToken = default);
}