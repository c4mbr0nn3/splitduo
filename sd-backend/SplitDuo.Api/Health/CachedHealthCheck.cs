using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SplitDuo.Api.Health;

public sealed class CachedHealthCheck(IHealthCheck inner, TimeProvider timeProvider, TimeSpan cacheDuration) : IHealthCheck
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _cacheLock = new();

    private HealthCheckResult _cachedResult;
    private DateTimeOffset _expiresAt;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        lock (_cacheLock)
        {
            if (now < _expiresAt)
            {
                return _cachedResult;
            }
        }

        // The gate is acquired outside the try so a cancelled waiter never runs
        // Release() for a permit it did not acquire (which would corrupt the gate).
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_cacheLock)
            {
                if (timeProvider.GetUtcNow() < _expiresAt)
                {
                    return _cachedResult;
                }
            }

            var result = await inner.CheckHealthAsync(context, cancellationToken);

            lock (_cacheLock)
            {
                _cachedResult = result;
                _expiresAt = timeProvider.GetUtcNow() + cacheDuration;
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}