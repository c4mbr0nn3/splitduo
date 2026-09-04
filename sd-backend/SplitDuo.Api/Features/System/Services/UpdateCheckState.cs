namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// In-memory singleton holding the latest-known version from the update check.
/// Thread-safe; null means unknown (no successful check yet).
/// </summary>
public class UpdateCheckState
{
    private readonly Lock _lock = new();
    private SemanticVersion? _latestVersion;

    public SemanticVersion? LatestVersion
    {
        get
        {
            lock (_lock)
                return _latestVersion;
        }
    }

    public void SetLatestVersion(SemanticVersion? version)
    {
        lock (_lock)
            _latestVersion = version;
    }
}