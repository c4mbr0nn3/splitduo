using System.Linq;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using SplitDuo.Api.Health;
using Xunit;

namespace SplitDuo.Tests.Unit;

public sealed class CachedHealthCheckTests
{
    private static HealthCheckContext Context()
    {
        var registration = new HealthCheckRegistration(
            "database",
            new CountingHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy())),
            HealthStatus.Unhealthy,
            ["db"],
            null);
        return new HealthCheckContext { Registration = registration };
    }

    private sealed class CountingHealthCheck(Func<CancellationToken, Task<HealthCheckResult>> behavior) : IHealthCheck
    {
        public int Calls;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            return behavior(cancellationToken);
        }
    }

    [Fact]
    public async Task ConcurrentCalls_WithinTtl_RunInnerCheckOnce()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new CountingHealthCheck(async ct =>
        {
            await release.Task.WaitAsync(ct);
            return HealthCheckResult.Healthy();
        });
        var check = new CachedHealthCheck(inner, new FakeTimeProvider(), TimeSpan.FromSeconds(10));

        var calls = Enumerable.Range(0, 20).Select(_ => check.CheckHealthAsync(Context())).ToArray();
        release.SetResult();
        await Task.WhenAll(calls);

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task AfterTtlExpiry_RunsInnerCheckAgain()
    {
        var time = new FakeTimeProvider();
        var inner = new CountingHealthCheck(_ => Task.FromResult(HealthCheckResult.Healthy()));
        var check = new CachedHealthCheck(inner, time, TimeSpan.FromSeconds(10));

        await check.CheckHealthAsync(Context());
        time.Advance(TimeSpan.FromSeconds(11));
        await check.CheckHealthAsync(Context());

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task UnhealthyResult_IsCached()
    {
        var inner = new CountingHealthCheck(_ => Task.FromResult(HealthCheckResult.Unhealthy("boom")));
        var check = new CachedHealthCheck(inner, new FakeTimeProvider(), TimeSpan.FromSeconds(10));

        var first = await check.CheckHealthAsync(Context());
        var second = await check.CheckHealthAsync(Context());

        Assert.Equal(1, inner.Calls);
        Assert.Equal(HealthStatus.Unhealthy, first.Status);
        Assert.Equal(HealthStatus.Unhealthy, second.Status);
    }

    [Fact]
    public async Task ThrowingCheck_IsNotCached()
    {
        var inner = new CountingHealthCheck(_ => throw new InvalidOperationException("transient"));
        var check = new CachedHealthCheck(inner, new FakeTimeProvider(), TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => check.CheckHealthAsync(Context()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => check.CheckHealthAsync(Context()));

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotCorruptTheGate()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new CountingHealthCheck(async ct =>
        {
            await release.Task.WaitAsync(ct);
            return HealthCheckResult.Healthy();
        });
        var check = new CachedHealthCheck(inner, new FakeTimeProvider(), TimeSpan.FromSeconds(10));

        var holder = check.CheckHealthAsync(Context());
        using var cts = new CancellationTokenSource();
        var waiter = check.CheckHealthAsync(Context(), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        release.SetResult();
        await holder;

        // Gate still usable: no SemaphoreFullException, cache populated by the holder.
        await check.CheckHealthAsync(Context());
    }
}