namespace SplitDuo.Core.Services;

/// <summary>
///     Policy for deciding whether the initial SystemAdmin credentials may be seeded.
///     Development always accepts (weak default kept for local dev); Production enforces
///     a minimum length and a blocklist of known-weak/default values.
/// </summary>
public static class BootstrapCredentialPolicy
{
    /// <summary>Minimum accepted password length in Production.</summary>
    public const int MinimumLength = 15;

    // Documented blocklist — do not expand without reason.
    private static readonly string[] WeakSubstrings =
    [
        "changeme",
        "password",
        "admin",
        "splitduo",
        "letmein",
        "qwerty",
        "123456",
        "secret",
        "root"
    ];

    private const string SeeDocs = "See docs/self-hosting.md.";

    public static BootstrapCredentialResult Evaluate(string? password, bool isProduction)
    {
        if (!isProduction) return BootstrapCredentialResult.Accepted;

        if (string.IsNullOrWhiteSpace(password))
        {
            return BootstrapCredentialResult.Rejected(
                $"SD_INITIAL_USER_PASSWORD must be set to a strong value (min {MinimumLength} characters) before the initial admin user can be seeded. {SeeDocs}");
        }

        if (password.Length < MinimumLength)
        {
            return BootstrapCredentialResult.Rejected(
                $"SD_INITIAL_USER_PASSWORD must be at least {MinimumLength} characters. {SeeDocs}");
        }

        foreach (var weak in WeakSubstrings)
        {
            if (password.Contains(weak, StringComparison.OrdinalIgnoreCase))
            {
                return BootstrapCredentialResult.Rejected(
                    $"SD_INITIAL_USER_PASSWORD is a known-weak/default password. Choose a strong, unique value (min {MinimumLength} characters). {SeeDocs}");
            }
        }

        return BootstrapCredentialResult.Accepted;
    }
}

public readonly record struct BootstrapCredentialResult(bool IsAccepted, string? ErrorMessage)
{
    public static BootstrapCredentialResult Accepted => new(true, null);
    public static BootstrapCredentialResult Rejected(string errorMessage) => new(false, errorMessage);
}