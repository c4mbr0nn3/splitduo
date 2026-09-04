namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Produces "update-available" notifications when the latest published version
/// is newer than the running assembly version (strict compare: current >= latest → none).
/// </summary>
public class UpdateNotificationProvider(
    UpdateCheckState updateCheckState,
    ICurrentVersionProvider currentVersionProvider,
    ILogger<UpdateNotificationProvider> logger) : INotificationProvider
{
    public const string NotificationType = "update-available";

    public Task<IReadOnlyList<AdminNotification>> GetPendingAsync(Guid userId, CancellationToken ct = default)
    {
        var latest = updateCheckState.LatestVersion;
        if (latest == null)
            return Task.FromResult<IReadOnlyList<AdminNotification>>([]);

        var current = currentVersionProvider.Current;
        if (current == null)
        {
            logger.LogWarning("Could not determine running assembly version; skipping update notification");
            return Task.FromResult<IReadOnlyList<AdminNotification>>([]);
        }

        // Strict compare: current >= latest → no update available
        if (current >= latest.Value)
            return Task.FromResult<IReadOnlyList<AdminNotification>>([]);

        AdminNotification notification = new(
            NotificationType,
            latest.Value.ToString(),
            new
            {
                current = current.Value.ToString(),
                latest = latest.Value.ToString(),
                releaseUrl = $"https://github.com/c4mbr0nn3/splitduo/releases/tag/v{latest.Value}"
            });

        return Task.FromResult<IReadOnlyList<AdminNotification>>([notification]);
    }
}