namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Strict semantic version (major.minor.patch). No pre-release or build metadata support.
/// Parsing and comparison are pure/static so tests don't need HTTP or DB.
/// Pre-release (e.g. "1.17.0-rc.1") and build-metadata tags are deliberately excluded:
/// release tags are strict x.y.z and pre-releases must never trigger update notifications.
/// </summary>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? input, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var parts = input.Trim().Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            return false;

        if (major < 0 || minor < 0 || patch < 0)
            return false;

        version = new SemanticVersion(major, minor, patch);
        return true;
    }

    public static SemanticVersion? ParseOrNull(string? input) =>
        TryParse(input, out var v) ? v : null;

    public int CompareTo(SemanticVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0) return minor;
        return Patch.CompareTo(other.Patch);
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}