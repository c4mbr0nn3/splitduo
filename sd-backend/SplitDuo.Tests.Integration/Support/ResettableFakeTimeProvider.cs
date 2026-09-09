using Microsoft.Extensions.Time;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// Test TimeProvider whose clock can be set both forward AND backward.
/// Used instead of Microsoft's FakeTimeProvider because FakeTimeProvider.SetUtcNow
/// throws ("Cannot go back in time") for backward jumps, which breaks the
/// reset-to-real-clock discipline in IntegrationTest.InitializeAsync once any
/// test has advanced the clock.
/// </summary>
public class ResettableFakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void SetUtcNow(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}