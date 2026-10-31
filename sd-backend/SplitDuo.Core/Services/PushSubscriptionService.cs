using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services;

/// <summary>
/// Short, log-safe endpoint prefix. Push endpoint URLs are long and may contain
/// identifying data — only ever log a 40-char prefix.
/// </summary>
internal static class PushLog
{
    internal static string ShortEndpoint(string endpoint) => endpoint[..Math.Min(40, endpoint.Length)];
}

public class PushSubscriptionService(
    ILogger<PushSubscriptionService> logger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IStringLocalizer<PushSubscriptionService> loc) : IPushSubscriptionService
{
    /// <summary>
    /// Every group event sends one request per subscription, each bounded by a 10s
    /// timeout, so an unbounded list turns a single notification into N outbound calls.
    /// Ten devices covers a phone, a tablet, a desktop and a few browsers per account.
    /// </summary>
    private const int MaxSubscriptionsPerUser = 10;

    private static bool IsValidEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    public async Task<Result> UpsertAsync(int userId, string endpoint, string p256dh, string auth, string? userAgent)
    {
        try
        {
            // The server POSTs to this URL later, so an unvalidated endpoint would be a
            // stored-request primitive: a user could register http://169.254.169.254/ or an
            // internal service name and have the app call it. Push services only ever hand
            // out https URLs, so anything else is rejected here, at the persistence boundary.
            if (!IsValidEndpoint(endpoint))
            {
                logger.LogWarning("Rejected push subscription endpoint for user {UserId}: not an absolute https URL", userId);
                return Result.BadRequest(loc["PushEndpointInvalid"]);
            }

            var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

            var existing = await unitOfWork.PushSubscriptions
                .FirstOrDefaultAsync(s => s.Endpoint == endpoint);

            if (existing != null)
            {
                if (existing.UserId != userId)
                {
                    logger.LogWarning(
                        "Push subscription endpoint {Endpoint} already registered for another user", PushLog.ShortEndpoint(endpoint));
                    return Result.Conflict(loc["PushAlreadyRegistered"]);
                }

                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.UserAgent = userAgent;
                existing.LastUsedAt = now;

                logger.LogDebug("Updated push subscription {Endpoint} for user {UserId}", PushLog.ShortEndpoint(endpoint), userId);
                return Result.Success();
            }

            // Cap applies to new devices only: a user at the limit can still re-register
            // an existing endpoint (handled above) so a rotated browser subscription heals.
            var subscriptionCount = await unitOfWork.PushSubscriptions
                .CountAsync(s => s.UserId == userId);

            if (subscriptionCount >= MaxSubscriptionsPerUser)
            {
                logger.LogWarning(
                    "User {UserId} reached the push subscription limit of {Max}; refusing a new device",
                    userId, MaxSubscriptionsPerUser);
                return Result.BadRequest(loc["PushSubscriptionLimitReached"]);
            }

            unitOfWork.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
                LastUsedAt = now,
                FailureCount = 0,
            });

            logger.LogDebug("Created push subscription {Endpoint} for user {UserId}", PushLog.ShortEndpoint(endpoint), userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upsert push subscription {Endpoint}", PushLog.ShortEndpoint(endpoint));
            return Result.InternalServerError("Failed to upsert push subscription");
        }
    }

    public async Task<Result> DeleteAsync(int userId, string endpoint)
    {
        try
        {
            var subscription = await unitOfWork.PushSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint);

            if (subscription == null)
            {
                logger.LogDebug("Push subscription {Endpoint} not found for user {UserId}", PushLog.ShortEndpoint(endpoint), userId);
                return Result.NotFound(loc["PushSubscriptionNotFound"]);
            }

            // Hard delete — the unique index on Endpoint must not hold stale rows,
            // otherwise re-subscription after DELETE/410-purge would violate it (23505).
            unitOfWork.PushSubscriptions.Remove(subscription);

            logger.LogDebug("Deleted push subscription {Endpoint} for user {UserId}", PushLog.ShortEndpoint(endpoint), userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete push subscription {Endpoint}", PushLog.ShortEndpoint(endpoint));
            return Result.InternalServerError("Failed to delete push subscription");
        }
    }

    public async Task<Result<IReadOnlyList<PushSubscription>>> GetByUserAsync(int userId)
    {
        try
        {
            var subscriptions = await unitOfWork.PushSubscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Result<IReadOnlyList<PushSubscription>>.Success(subscriptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get push subscriptions for user {UserId}", userId);
            return Result<IReadOnlyList<PushSubscription>>.InternalServerError("Failed to get push subscriptions");
        }
    }
}
