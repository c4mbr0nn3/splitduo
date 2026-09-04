namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Pure/static logic for managing the per-user dismissed-notification list:
/// stale-entry pruning (semver-based) and deduplication. No HTTP or DB needed for tests.
/// </summary>
public static class NotificationDismissalHelper
{
    public const int MaxDismissedEntries = 64;
    public const int MaxTypeLength = 64;
    public const int MaxTargetKeyLength = 128;

    /// <summary>Prunes stale update-available dismissals and dedupes the list in place.</summary>
    public static void PruneAndDedupe(
        List<Core.Domain.Entities.DismissedNotification> dismissed,
        string currentVersionString)
    {
        var current = SemanticVersion.ParseOrNull(currentVersionString);

        var seen = new HashSet<(string, string)>();
        dismissed.RemoveAll(entry =>
        {
            // Dedupe
            if (!seen.Add((entry.Type, entry.TargetKey)))
                return true;

            // Stale cleanup: update-available dismissals whose targetKey parses to
            // a semver <= the running version are no longer needed
            if (entry.Type == UpdateNotificationProvider.NotificationType &&
                current != null &&
                SemanticVersion.ParseOrNull(entry.TargetKey) is { } target &&
                target <= current)
            {
                return true;
            }

            return false;
        });

        // Hard cap: keep the most recent entries (insertion order = recency order)
        if (dismissed.Count > MaxDismissedEntries)
            dismissed.RemoveRange(0, dismissed.Count - MaxDismissedEntries);
    }

    /// <summary>Returns true if the notification is dismissed by the given list. Tolerates a null list.</summary>
    public static bool IsDismissed(
        IReadOnlyList<Core.Domain.Entities.DismissedNotification>? dismissed,
        string type,
        string targetKey) =>
        dismissed?.Any(d => d.Type == type && d.TargetKey == targetKey) == true;

    /// <summary>Returns true if a notification with the given type and target key
    /// is currently pending (i.e. the dismissal refers to a real, live notification).</summary>
    public static bool IsPendingNotification(
        IReadOnlyList<AdminNotification> pending,
        string type,
        string targetKey) =>
        pending.Any(n => n.Type == type && n.TargetKey == targetKey);
}