using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Localization;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services;

public class PushNotificationService(
    ILogger<PushNotificationService> logger,
    IOptions<PushOptions> pushOptions,
    IPushSubscriptionService pushSubscriptionService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    PushHttpClient pushHttpClient,
    IStringLocalizer<PushNotificationService> loc) : IPushNotificationService
{
    private const int SendTimeoutSeconds = 10;
    private const int GoneStatusCode = 410;
    private const int NotFoundStatusCode = 404;
    private const int MaxFailureCount = 5;

    /// <summary>
    /// Result of one network send, before any row is touched.
    /// </summary>
    private enum SendOutcome
    {
        /// <summary>Delivered — the subscription is healthy.</summary>
        Sent,

        /// <summary>The push service says the subscription is gone (410/404).</summary>
        Gone,

        /// <summary>Transient failure (timeout, 5xx, network) — count it.</summary>
        Failed,

        /// <summary>The caller cancelled, so nothing was learned about the endpoint.</summary>
        Skipped,
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Result> SendAsync(int userId, PushPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = pushOptions.Value;
            if (!options.IsConfigured)
                return Result.Success();

            // Recipient language drives the body text, not the caller's culture.
            var user = await unitOfWork.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, cancellationToken);
            if (user == null)
                return Result.Success();

            var language = SupportedLanguages.Normalize(user.Settings.UiLanguage);
            var body = string.Format(loc[payload.BodyKey, Culture(language)].Value, payload.BodyArgs);

            var json = JsonSerializer.Serialize(new
            {
                title = payload.Title,
                body,
                url = payload.Url,
                tag = payload.Tag,
            }, JsonOptions);

            var subscriptionsResult = await pushSubscriptionService.GetByUserAsync(userId);
            if (subscriptionsResult.IsFailure)
                return Result.Success();

            var subscriptions = subscriptionsResult.Value!;
            if (subscriptions.Count == 0)
                return Result.Success();

            var vapidDetails = new WebPush.VapidDetails(
                options.Subject, options.VapidPublicKey, options.VapidPrivateKey);

            // Sends are independent network calls (fresh WebPushClient per send) — run them
            // concurrently. They must not touch the DbContext while doing so: EF Core forbids
            // concurrent operations on one context, and every mutation here would race. The
            // tasks therefore only report an outcome; rows are updated below, sequentially.
            var tasks = subscriptions.Select(subscription => SendToSubscriptionAsync(
                subscription, json, vapidDetails, cancellationToken)).ToList();

            var outcomes = await Task.WhenAll(tasks);

            if (ApplyOutcomes(subscriptions, outcomes))
                await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catastrophic error sending push notifications for user {UserId}", userId);
            return Result.InternalServerError(loc["PushSendFailed"]);
        }
    }

    private async Task<SendOutcome> SendToSubscriptionAsync(
        PushSubscription subscription,
        string json,
        WebPush.VapidDetails vapidDetails,
        CancellationToken cancellationToken)
    {
        try
        {
            // WebPushClient is stateful and not thread-safe — a fresh instance per send.
            // The HttpClient is shared: it carries the redirect and address guards, and
            // WebPushClient does not dispose a client it did not create.
            var client = new WebPush.WebPushClient(pushHttpClient.Client);
            var wpSubscription = new WebPush.PushSubscription(
                subscription.Endpoint, subscription.P256dh, subscription.Auth);

            // Bound each send with a 10s timeout (linked from the caller's token).
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(SendTimeoutSeconds));

            try
            {
                await client.SendNotificationAsync(wpSubscription, json, vapidDetails, linked.Token);
                return SendOutcome.Sent;
            }
            catch (WebPush.WebPushException ex)
            {
                if ((int)ex.StatusCode is GoneStatusCode or NotFoundStatusCode)
                    return SendOutcome.Gone;

                logger.LogWarning("Push send to {Endpoint} failed with status {StatusCode}",
                    PushLog.ShortEndpoint(subscription.Endpoint), (int)ex.StatusCode);
                return SendOutcome.Failed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller gave up (request aborted, tab closed) — the device told us
                // nothing, so this must not count as a failure. Without this clause the
                // exception fell into the generic catch as Failed: a phantom increment
                // (or purge, at the fifth) stayed in the shared change tracker, and the
                // next tokenless SaveChangesAsync in the same scope — every controller here
                // saves that way — flushed it as soon as any call site started passing a token.
                return SendOutcome.Skipped;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                // Our own send timeout — the endpoint really did fail to answer.
                logger.LogWarning("Push send to {Endpoint} timed out after {Timeout}s",
                    PushLog.ShortEndpoint(subscription.Endpoint), SendTimeoutSeconds);
                return SendOutcome.Failed;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unexpected failure sending push to {Endpoint}",
                    PushLog.ShortEndpoint(subscription.Endpoint));
                return SendOutcome.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled while preparing the send (before the inner try) — nothing was
            // learned about the endpoint.
            return SendOutcome.Skipped;
        }
    }

    /// <summary>
    /// Applies each send's outcome to its subscription row. Runs after all sends have
    /// completed and never queries the DbContext, so it cannot race.
    /// Returns true when a row actually changed, so the caller only pays for a save —
    /// and only flushes the caller's own pending changes — when it matters.
    /// </summary>
    private bool ApplyOutcomes(IReadOnlyList<PushSubscription> subscriptions, SendOutcome[] outcomes)
    {
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var mutated = false;

        for (var i = 0; i < subscriptions.Count; i++)
        {
            var subscription = subscriptions[i];

            switch (outcomes[i])
            {
                case SendOutcome.Sent:
                    if (subscription.FailureCount != 0)
                    {
                        subscription.FailureCount = 0;
                        mutated = true;
                    }

                    if (subscription.LastUsedAt != now)
                    {
                        subscription.LastUsedAt = now;
                        mutated = true;
                    }

                    break;

                case SendOutcome.Gone:
                    unitOfWork.PushSubscriptions.Remove(subscription);
                    mutated = true;
                    logger.LogInformation("Purged dead push subscription {Endpoint}",
                        PushLog.ShortEndpoint(subscription.Endpoint));
                    break;

                case SendOutcome.Skipped:
                    // Cancelled by the caller: nothing was learned about the endpoint,
                    // so the row is left exactly as it was (and nothing is saved).
                    break;

                case SendOutcome.Failed:
                    subscription.FailureCount++;
                    mutated = true;

                    if (subscription.FailureCount >= MaxFailureCount)
                    {
                        unitOfWork.PushSubscriptions.Remove(subscription);
                        logger.LogInformation(
                            "Push subscription {Endpoint} reached max failure count, purging",
                            PushLog.ShortEndpoint(subscription.Endpoint));
                    }
                    else
                    {
                        logger.LogDebug("Recorded failure {FailureCount}/{Max} for push subscription {Endpoint}",
                            subscription.FailureCount, MaxFailureCount, PushLog.ShortEndpoint(subscription.Endpoint));
                    }

                    break;
            }
        }

        return mutated;
    }

    private static CultureInfo Culture(string language) => SupportedLanguages.Cultures
        .First(c => c.TwoLetterISOLanguageName == language);
}