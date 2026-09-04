using SplitDuo.Api.Features.System.Services;
using SplitDuo.Core.Domain.Entities;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class NotificationDismissalHelperTests
{
    private const string CurrentVersion = "1.16.0";

    private static DismissedNotification Entry(string type, string targetKey) =>
        new() { Type = type, TargetKey = targetKey };

    #region PruneAndDedupe

    [Fact]
    public void PruneAndDedupe_RemovesStaleUpdateDismissals()
    {
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "1.15.0"), // <= current → stale
            Entry("update-available", "1.16.0"), // == current → stale
            Entry("update-available", "1.17.0"), // > current → keep
        };

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Single(list);
        Assert.Equal("1.17.0", list[0].TargetKey);
    }

    [Fact]
    public void PruneAndDedupe_KeepsNonUpdateDismissals()
    {
        var list = new List<DismissedNotification>
        {
            Entry("maintenance", "2026-09-01"),
            Entry("other-type", "0.0.1"),
        };

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void PruneAndDedupe_DedupesIdenticalEntries()
    {
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "1.17.0"),
            Entry("update-available", "1.17.0"),
            Entry("maintenance", "x"),
            Entry("maintenance", "x"),
        };

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Equal(2, list.Count);
        Assert.Single(list, e => e.Type == "update-available");
        Assert.Single(list, e => e.Type == "maintenance");
    }

    [Fact]
    public void PruneAndDedupe_UnparseableTargetKey_KeptForUpdates()
    {
        // targetKey that doesn't parse as semver must NOT be pruned (only parseable
        // stale semvers are cleaned up)
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "not-a-version"),
        };

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Single(list);
    }

    [Fact]
    public void PruneAndDedupe_CapsListAtMaxEntries_KeepsMostRecent()
    {
        var list = Enumerable.Range(0, NotificationDismissalHelper.MaxDismissedEntries + 6)
            .Select(i => Entry("maintenance", $"key-{i}"))
            .ToList();

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Equal(NotificationDismissalHelper.MaxDismissedEntries, list.Count);
        Assert.Equal("key-6", list[0].TargetKey); // oldest dropped, most recent kept
        Assert.Equal($"key-{NotificationDismissalHelper.MaxDismissedEntries + 5}", list[^1].TargetKey);
    }

    [Fact]
    public void PruneAndDedupe_MixedList_PrunesStaleKeepsNewerAndOtherTypes()
    {
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "1.15.0"), // stale (<= current)
            Entry("update-available", "1.17.0"), // newer → keep
            Entry("maintenance", "2026-09-01"),  // other type → keep
        };

        NotificationDismissalHelper.PruneAndDedupe(list, CurrentVersion);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.Type == "update-available" && e.TargetKey == "1.17.0");
        Assert.Contains(list, e => e.Type == "maintenance");
    }

    #endregion

    #region IsDismissed

    [Fact]
    public void IsDismissed_MatchingTypeAndTargetKey_ReturnsTrue()
    {
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "1.17.0"),
        };

        Assert.True(NotificationDismissalHelper.IsDismissed(list, "update-available", "1.17.0"));
    }

    [Fact]
    public void IsDismissed_DifferentTargetKey_ReturnsFalse()
    {
        var list = new List<DismissedNotification>
        {
            Entry("update-available", "1.17.0"),
        };

        Assert.False(NotificationDismissalHelper.IsDismissed(list, "update-available", "1.18.0"));
        Assert.False(NotificationDismissalHelper.IsDismissed(list, "other", "1.17.0"));
    }

    [Fact]
    public void IsDismissed_NullList_ReturnsFalse()
    {
        // Legacy rows may deserialize DismissedNotifications as null (POST guards this;
        // GET relies on IsDismissed being null-tolerant — lock that in)
        Assert.False(NotificationDismissalHelper.IsDismissed(null, "update-available", "1.17.0"));
    }

    #endregion

    #region IsPendingNotification

    [Fact]
    public void IsPendingNotification_MatchAndNoMatch()
    {
        var pending = new List<AdminNotification>
        {
            new("update-available", "1.17.0", null),
        };

        Assert.True(NotificationDismissalHelper.IsPendingNotification(pending, "update-available", "1.17.0"));
        Assert.False(NotificationDismissalHelper.IsPendingNotification(pending, "update-available", "1.18.0"));
        Assert.False(NotificationDismissalHelper.IsPendingNotification(pending, "other", "1.17.0"));
    }

    #endregion
}