namespace SplitDuo.Core.Common;

/// <summary>
/// Single source of truth for how an email address is stored and compared.
/// The database unique index is built on <c>lower(email)</c>, so every write
/// and every lookup must pass through here.
/// </summary>
public static class EmailNormalizer
{
    public static string Normalize(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();
}
