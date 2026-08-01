using System.Globalization;

namespace SplitDuo.Core.Localization;

/// <summary>
/// Single source of truth for supported UI languages.
/// Adding a language is a one-line edit: add it to <see cref="All"/>.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>The default language code.</summary>
    public const string Default = "en";

    /// <summary>All supported language codes.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(["en", "it"]);

    /// <summary>CultureInfo instances for each supported language.</summary>
    public static readonly CultureInfo[] Cultures = All.Select(c => new CultureInfo(c)).ToArray();

    /// <summary>Returns true if <paramref name="language"/> is a supported language code.</summary>
    public static bool IsSupported(string? language) =>
        language != null && All.Contains(language);

    /// <summary>
    /// Returns <paramref name="language"/> if supported, otherwise <see cref="Default"/>.
    /// </summary>
    public static string Normalize(string? language) =>
        IsSupported(language) ? language! : Default;
}
