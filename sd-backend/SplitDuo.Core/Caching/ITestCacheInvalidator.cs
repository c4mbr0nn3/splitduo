namespace SplitDuo.Core.Caching;

/// <summary>
/// Test-only hook to clear the entire cache between integration tests.
/// Lives in Core (not the test project) because the implementation is in Core.
/// </summary>
public interface ITestCacheInvalidator
{
    Task ClearAllAsync();
}
