using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace SplitDuo.Core.Caching;

/// <summary>
/// Invalidates HybridCache entries by tag. Invalidation failures are logged and
/// swallowed — data is already persisted, so the cache TTL is the safety net.
/// </summary>
public class HybridCacheInvalidator : ICacheInvalidator, ITestCacheInvalidator
{
    private readonly HybridCache _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HybridCacheInvalidator> _logger;

    public HybridCacheInvalidator(
        HybridCache cache,
        IMemoryCache memoryCache,
        ILogger<HybridCacheInvalidator> logger)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task InvalidateGroupAsync(string groupGuid, CancellationToken ct = default)
    {
        try
        {
            // Normalize to lowercase Guid string to match cache keys from BalancesService
            // (which uses Guid.TryParse().ToString() — always lowercase)
            var normalized = Guid.TryParse(groupGuid, out var g) ? g.ToString() : groupGuid;
            await _cache.RemoveByTagAsync($"group:{normalized}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for group {GroupGuid}", groupGuid);
        }
    }

    public async Task InvalidateUserAsync(Guid userGuid, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveByTagAsync($"user:{userGuid}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache for user {UserGuid}", userGuid);
        }
    }

    public Task ClearAllAsync()
    {
        // HybridCache has no clear-all API; the underlying store is a MemoryCache
        // (registered via AddMemoryCache), so compact it fully to drop every entry.
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0);
        }
        else
        {
            _logger.LogWarning("IMemoryCache is not a MemoryCache instance; cache clear skipped");
        }

        return Task.CompletedTask;
    }
}
