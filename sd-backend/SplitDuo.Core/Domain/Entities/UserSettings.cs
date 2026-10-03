namespace SplitDuo.Core.Domain.Entities;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Per-user UI preferences. Stored as jsonb on the users table.
/// Add new settings here with a default initializer, but any new <b>required</b> member
/// (scalar or collection) MUST ship a backfill migration for existing rows: EF Core 10
/// materializes an absent JSON key as null, ignores the CLR initializer, and throws
/// NullRequiredComplexProperty on save (dotnet/efcore#38625; fixed only in EF Core 12).
/// See migration 20261003083142_BackfillUserSettingsRequiredKeys.
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