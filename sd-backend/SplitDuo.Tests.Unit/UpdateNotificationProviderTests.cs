using Microsoft.Extensions.Logging.Abstractions;
using SplitDuo.Api.Features.System.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class UpdateNotificationProviderTests
{
    private sealed class StubCurrentVersionProvider : ICurrentVersionProvider
    {
        public SemanticVersion? Current { get; set; }
        public string CurrentString => Current?.ToString() ?? "0.0.0";
    }

    private static UpdateNotificationProvider CreateProvider(
        UpdateCheckState state, SemanticVersion? current) =>
        new(state, new StubCurrentVersionProvider { Current = current },
            NullLogger<UpdateNotificationProvider>.Instance);

    private static string? PayloadProperty(object? payload, string name) =>
        payload?.GetType().GetProperty(name)?.GetValue(payload)?.ToString();

    [Fact]
    public void NoNotifications_WhenLatestUnknown()
    {
        var result = CreateProvider(new UpdateCheckState(), new SemanticVersion(1, 16, 0))
            .GetPendingAsync(Guid.NewGuid()).Result;

        Assert.Empty(result);
    }

    [Fact]
    public void NoNotifications_WhenCurrentVersionUnknown()
    {
        var state = new UpdateCheckState();
        state.SetLatestVersion(new SemanticVersion(1, 17, 0));

        var result = CreateProvider(state, null).GetPendingAsync(Guid.NewGuid()).Result;

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(1, 17, 0, 1, 17, 0)] // equal → none
    [InlineData(1, 18, 0, 1, 17, 0)] // current newer → none
    public void NoNotifications_WhenCurrentGteLatest(int cm, int cmi, int cp, int lm, int lmi, int lp)
    {
        var state = new UpdateCheckState();
        state.SetLatestVersion(new SemanticVersion(lm, lmi, lp));

        var result = CreateProvider(state, new SemanticVersion(cm, cmi, cp))
            .GetPendingAsync(Guid.NewGuid()).Result;

        Assert.Empty(result);
    }

    [Fact]
    public void UpdateAvailable_ReturnsWellShapedNotification()
    {
        var state = new UpdateCheckState();
        state.SetLatestVersion(new SemanticVersion(1, 17, 0));

        var result = CreateProvider(state, new SemanticVersion(1, 16, 0))
            .GetPendingAsync(Guid.NewGuid()).Result;

        var notification = Assert.Single(result);
        Assert.Equal(UpdateNotificationProvider.NotificationType, notification.Type);
        Assert.Equal("1.17.0", notification.TargetKey);
        Assert.Equal("1.16.0", PayloadProperty(notification.Payload, "current"));
        Assert.Equal("1.17.0", PayloadProperty(notification.Payload, "latest"));
        Assert.Equal(
            "https://github.com/c4mbr0nn3/splitduo/releases/tag/v1.17.0",
            PayloadProperty(notification.Payload, "releaseUrl"));
    }
}