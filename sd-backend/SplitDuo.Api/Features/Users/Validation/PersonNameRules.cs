using System.Text;
using System.Text.RegularExpressions;

namespace SplitDuo.Api.Features.Users.Validation;

public static partial class PersonNameRules
{
    public const int MaxLength = 100;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true; // optional field / clear

        try
        {
            var normalized = Normalize(value);
            return normalized.Length <= MaxLength && AllowedPattern().IsMatch(normalized);
        }
        catch (ArgumentException)
        {
            return false; // invalid UTF-16
        }
    }

    public static string Normalize(string value) =>
        WhitespacePattern().Replace(
            value.Normalize(NormalizationForm.FormC).Trim(), " ");

    [GeneratedRegex(@"^[\p{L}\p{M}\p{Zs}'’ʼ·・.\-‐‑–—]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
