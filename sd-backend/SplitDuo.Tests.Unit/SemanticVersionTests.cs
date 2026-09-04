using SplitDuo.Api.Features.System.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class SemanticVersionTests
{
    #region TryParse

    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("0.0.0", 0, 0, 0)]
    [InlineData("10.20.30", 10, 20, 30)]
    [InlineData("  1.2.3  ", 1, 2, 3)]
    public void TryParse_ValidInput_ReturnsTrue(string input, int major, int minor, int patch)
    {
        var ok = SemanticVersion.TryParse(input, out var version);
        Assert.True(ok);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-beta")]
    [InlineData("v1.2.3")]
    [InlineData("latest")]
    [InlineData("a.b.c")]
    [InlineData("-1.2.3")]
    [InlineData("1..3")]
    public void TryParse_MalformedInput_ReturnsFalse(string? input)
    {
        Assert.False(SemanticVersion.TryParse(input, out _));
        Assert.False(SemanticVersion.ParseOrNull(input) is not null);
    }

    #endregion

    #region Comparison

    [Theory]
    [InlineData("1.2.3", "1.2.3", 0)]
    [InlineData("1.2.3", "1.2.4", -1)]
    [InlineData("1.2.4", "1.2.3", 1)]
    [InlineData("1.9.9", "2.0.0", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("9.0.0", "10.0.0", -1)]
    public void CompareTo_Ordering(string left, string right, int expectedSign)
    {
        var l = SemanticVersion.ParseOrNull(left)!.Value;
        var r = SemanticVersion.ParseOrNull(right)!.Value;
        var result = Math.Sign(l.CompareTo(r));
        Assert.Equal(expectedSign, result);
    }

    [Theory]
    [InlineData("1.16.0", "1.17.0", false)] // update available → not up to date
    [InlineData("1.17.0", "1.16.0", true)]  // ahead of latest → up to date
    [InlineData("1.16.0", "1.16.0", true)]  // equal → up to date
    [InlineData("0.9.0", "1.0.0", false)]
    public void CurrentGteLatest_StrictCompare(string current, string latest, bool expected)
    {
        var c = SemanticVersion.ParseOrNull(current)!.Value;
        var l = SemanticVersion.ParseOrNull(latest)!.Value;
        Assert.Equal(expected, c >= l);
        Assert.Equal(!expected, l > c);
    }

    #endregion
}