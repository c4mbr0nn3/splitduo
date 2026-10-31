using SplitDuo.Core.Common;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData("  Alice@Example.COM  ", "alice@example.com")]
    [InlineData("bob@localhost", "bob@localhost")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_TrimsAndLowercases(string? input, string expected)
    {
        Assert.Equal(expected, EmailNormalizer.Normalize(input));
    }

    [Fact]
    public void UserEmailSetter_StoresNormalizedValue()
    {
        var user = new SplitDuo.Core.Domain.Entities.User { Email = "  Mixed@Case.Local  " };

        Assert.Equal("mixed@case.local", user.Email);
    }
}
