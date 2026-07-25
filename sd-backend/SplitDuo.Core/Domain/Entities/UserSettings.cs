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

    /// <summary>ISO 639-1 code. Only "en" accepted in v1; widened when i18n lands.</summary>
    [MaxLength(8)]
    public string UiLanguage { get; set; } = "en";
}
