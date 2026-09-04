using System.Text;
using System.Text.Json;

namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Fetches the latest published version of SplitDuo from public release feeds.
/// Primary source: Docker Hub tags. Fallback (on any primary failure):
/// the VERSION file on GitHub raw. No identifying data is sent (no custom headers).
/// </summary>
public interface IVersionFeedService
{
    /// <summary>Latest published version, or null if unknown (all sources failed).</summary>
    Task<SemanticVersion?> GetLatestVersionAsync(CancellationToken ct = default);
}

public class VersionFeedService(
    IHttpClientFactory httpClientFactory,
    ILogger<VersionFeedService> logger) : IVersionFeedService
{
    private const string DockerHubTagsUrl = "https://hub.docker.com/v2/repositories/j1mm0/splitduo/tags?page_size=100";
    private const string GitHubVersionUrl = "https://raw.githubusercontent.com/c4mbr0nn3/splitduo/main/VERSION";
    private const int MaxDockerHubPages = 5;
    private const int MaxResponseBytes = 512 * 1024; // char cap while streaming (~bytes for JSON)

    public async Task<SemanticVersion?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        var fromDockerHub = await GetLatestFromDockerHubAsync(ct);
        if (fromDockerHub != null)
            return fromDockerHub;

        logger.LogWarning("Docker Hub version feed failed, falling back to GitHub VERSION file");
        return await GetLatestFromGitHubAsync(ct);
    }

    /// <summary>
    /// Reads the response body with a hard size cap. Returns null (treated as a failed
    /// source) when Content-Length exceeds the cap or the stream yields more chars than
    /// the cap. Char-based cap is a documented approximation (JSON feeds are ASCII-dominant).
    /// </summary>
    private static async Task<string?> ReadCappedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var buffer = new char[8192];
        var sb = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            if (read == 0)
                break;
            sb.Append(buffer, 0, read);
            if (sb.Length > MaxResponseBytes)
                return null;
        }
        return sb.ToString();
    }

    private async Task<SemanticVersion?> GetLatestFromDockerHubAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("VersionFeed");

            SemanticVersion? best = null;
            string? nextUrl = DockerHubTagsUrl;
            for (var page = 0; page < MaxDockerHubPages && nextUrl != null; page++)
            {
                using var response = await client.GetAsync(
                    nextUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Docker Hub tags request returned {StatusCode}",
                        (int)response.StatusCode);
                    break; // keep best-so-far; page 1 failure → best stays null → GitHub fallback
                }

                var json = await ReadCappedAsync(response, ct);
                if (json == null)
                {
                    logger.LogWarning("Docker Hub tags response exceeded {MaxBytes} bytes", MaxResponseBytes);
                    break;
                }

                var pageBest = VersionFeedParser.ParseDockerHubTags(json);
                if (pageBest != null && (best == null || pageBest > best))
                    best = pageBest;

                nextUrl = VersionFeedParser.TryParseNextLink(json, out var next) ? next : null;
            }

            if (best == null)
                logger.LogWarning("Docker Hub tags response contained no valid semver tags");

            return best;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Docker Hub version feed request failed");
            return null;
        }
    }

    private async Task<SemanticVersion?> GetLatestFromGitHubAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("VersionFeed");

            using var response = await client.GetAsync(
                GitHubVersionUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub VERSION request returned {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var content = await ReadCappedAsync(response, ct);
            if (content == null)
            {
                logger.LogWarning("GitHub VERSION response exceeded {MaxBytes} bytes", MaxResponseBytes);
                return null;
            }

            var version = VersionFeedParser.ParseVersionFile(content);
            if (version == null)
                logger.LogWarning("GitHub VERSION file content could not be parsed as semver");

            return version;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "GitHub VERSION fallback request failed");
            return null;
        }
    }
}