namespace SplitDuo.Core.Domain.Entities;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Per-user UI preferences. Stored as jsonb on the users table.
/// Add new settings here with a default initializer — no migration needed
/// for additive changes (System.Text.Json uses CLR defaults for missing keys).
/// </summary>
public class UserSettings
{
    /// <summary>"light" | "dark" | "auto" (follows OS)</summary>
    [MaxLength(16)]
    public string Theme { get; set; } = "auto";

    /// <summary>ISO 639-1 code. Accepts "en" (default) or "it".</summary>
    [MaxLength(8)]
    public string UiLanguage { get; set; } = "en";

    /// <summary>
    /// Admin notifications the user has dismissed (keyed by Type + TargetKey).
    /// Additive jsonb change — no migration required.
    /// </summary>
    public List<DismissedNotification> DismissedNotifications { get; set; } = [];
}

/// <summary>
/// A dismissed admin notification, identified by its type and target key.
/// Stored as part of the UserSettings jsonb blob.
/// </summary>
public class DismissedNotification
{
    /// <summary>Notification type, e.g. "update-available".</summary>
    [MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Target key, e.g. the latest version string for update notifications.</summary>
    [MaxLength(128)]
    public string TargetKey { get; set; } = string.Empty;
}