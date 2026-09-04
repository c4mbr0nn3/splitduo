using Microsoft.Extensions.Options;
using SplitDuo.Core.Options;

namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Periodically checks public release feeds for a newer SplitDuo version.
/// First check ~1 minute after startup, then every 24h with ±30min jitter
/// (PeriodicTimer for the base period + an extra jittered delay per tick).
/// Honors SD_UPDATE_CHECK_DISABLED (does nothing, logs once at startup).
/// Failures are logged and never retried before the next tick.
/// </summary>
public class UpdateCheckBackgroundService(
    IVersionFeedService versionFeedService,
    UpdateCheckState updateCheckState,
    ICurrentVersionProvider currentVersionProvider,
    IOptions<UpdateCheckOptions> options,
    ILogger<UpdateCheckBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BaseInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxJitter = TimeSpan.FromMinutes(30); // ±30 min around 24h
    private SemanticVersion CurrentVersion => currentVersionProvider.Current ?? new SemanticVersion(0, 0, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Disabled)
        {
            logger.LogInformation("Update check is disabled via SD_UPDATE_CHECK_DISABLED");
            return;
        }

        logger.LogInformation("Update check started (current version {CurrentVersion})", CurrentVersion.ToString());

        try
        {
            // First check ~1 minute after startup
            await Task.Delay(InitialDelay, stoppingToken);
            await CheckForUpdateAsync(stoppingToken);

            using var timer = new PeriodicTimer(BaseInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Jitter: extra delay in [-30min, +30min] around the base period
                var jitter = TimeSpan.FromSeconds(
                    (Random.Shared.NextDouble() * 2 - 1) * MaxJitter.TotalSeconds);
                if (jitter > TimeSpan.Zero)
                    await Task.Delay(jitter, stoppingToken);

                await CheckForUpdateAsync(stoppingToken);

                if (jitter < TimeSpan.Zero)
                    await Task.Delay(-jitter, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task CheckForUpdateAsync(CancellationToken ct)
    {
        try
        {
            var latest = await versionFeedService.GetLatestVersionAsync(ct);
            if (latest == null)
            {
                logger.LogWarning("Update check could not determine latest version; keeping last known state");
                return;
            }

            updateCheckState.SetLatestVersion(latest);

            // Strict compare: current >= latest → no update
            if (CurrentVersion >= latest)
            {
                logger.LogDebug("No update available (current {CurrentVersion}, latest {LatestVersion})",
                    CurrentVersion.ToString(), latest.ToString());
            }
            else
            {
                logger.LogInformation("Update available: {LatestVersion} (current {CurrentVersion})",
                    latest.ToString(), CurrentVersion.ToString());
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update check failed; no retry before next tick");
        }
    }
}