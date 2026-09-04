namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Future-proof seam for admin notifications. Each provider produces pending
/// notifications for a user; the controller aggregates all registered providers.
/// </summary>
public interface INotificationProvider
{
    Task<IReadOnlyList<AdminNotification>> GetPendingAsync(Guid userId, CancellationToken ct = default);
}

public record AdminNotification(string Type, string TargetKey, object? Payload);