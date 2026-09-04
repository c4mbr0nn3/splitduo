using System.Text.Json;

namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Pure parsing logic for version feeds — separated from HTTP concerns so it
/// can be unit-tested with raw JSON/string inputs.
/// </summary>
public static class VersionFeedParser
{
    /// <summary>
    /// Parses Docker Hub tags JSON ({ "results": [ { "name": "1.2.3" }, ... ] }) from a
    /// single page and returns the highest semver tag on that page. Ignores the "latest"
    /// tag and any non-semver tags. Multi-page walking (bounded, host-guarded) happens in
    /// VersionFeedService via TryParseNextLink.
    /// Returns null for malformed JSON, missing/empty results, or no valid semver tags.
    /// </summary>
    public static SemanticVersion? ParseDockerHubTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
                return null;

            SemanticVersion? highest = null;
            foreach (var result in results.EnumerateArray())
            {
                if (result.ValueKind != JsonValueKind.Object ||
                    !result.TryGetProperty("name", out var name) ||
                    name.ValueKind != JsonValueKind.String)
                    continue;

                var tag = name.GetString();
                if (string.IsNullOrEmpty(tag) || tag.Equals("latest", StringComparison.OrdinalIgnoreCase))
                    continue;

                var version = SemanticVersion.ParseOrNull(tag);
                if (version != null && (highest == null || version > highest))
                    highest = version;
            }

            return highest;
        }
    }

    /// <summary>
    /// Parses a plain-text VERSION file (e.g. "1.16.0\n"). Returns null for malformed input.
    /// </summary>
    public static SemanticVersion? ParseVersionFile(string? content) =>
        SemanticVersion.ParseOrNull(content);

    /// <summary>
    /// Extracts the Docker Hub "next" page link from a tags response. Returns false
    /// (and null) when the link is missing, null, not a string, or not an absolute
    /// HTTPS URL on hub.docker.com — never follow links off-host (SSRF guard).
    /// </summary>
    public static bool TryParseNextLink(string? json, out string? nextUrl)
    {
        nextUrl = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("next", out var next) ||
                next.ValueKind != JsonValueKind.String)
                return false;

            var value = next.GetString();
            if (string.IsNullOrEmpty(value))
                return false;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                uri.Host != "hub.docker.com")
                return false;

            nextUrl = value;
            return true;
        }
    }
}