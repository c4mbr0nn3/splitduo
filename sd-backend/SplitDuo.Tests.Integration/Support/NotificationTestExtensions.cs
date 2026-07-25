using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Helpers for inspecting enqueued email notifications during tests.
/// The Quartz sender hosted service is removed in the test host, so notifications
/// stay in the `notifications` table with SentAt = null. Plaintext tokens (invitation,
/// password reset) are only ever stored hashed in their respective token tables —
/// the raw value lives solely in the rendered email body URL, so tests must read it
/// from the notification body.
/// </summary>
public static class NotificationTestExtensions
{
    /// <summary>
    /// Returns all unsent notifications enqueued for the given recipient email,
    /// newest first.
    /// </summary>
    public static async Task<List<string>> GetEnqueuedBodiesAsync(
        IServiceProvider services, string toEmail)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Order by Id (auto-increment) rather than CreatedAt: CreatedAt is Unix seconds,
        // so two notifications created in the same second tie and order non-deterministically.
        var bodies = await db.Notifications
            .Where(n => n.To == toEmail && n.SentAt == null)
            .OrderByDescending(n => n.Id)
            .Select(n => n.Body)
            .ToListAsync();
        return bodies;
    }

    /// <summary>
    /// Extracts the raw token from the first enqueued notification for the given email.
    /// Supports both invitation URLs (`/invite/accept?token=...`) and password-reset
    /// URLs (`/reset-password?email=...&token=...`).
    /// </summary>
    public static async Task<string> ExtractTokenFromFirstNotificationAsync(
        IServiceProvider services, string toEmail)
    {
        var bodies = await GetEnqueuedBodiesAsync(services, toEmail);
        if (bodies.Count == 0)
            throw new InvalidOperationException(
                $"No enqueued notification found for {toEmail}");

        var match = Regex.Match(bodies[0], @"[?&]token=([^""&]+)");
        if (!match.Success)
            throw new InvalidOperationException(
                $"No token query parameter found in notification body for {toEmail}");

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}