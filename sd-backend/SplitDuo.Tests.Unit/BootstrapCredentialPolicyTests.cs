using SplitDuo.Core.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class BootstrapCredentialPolicyTests
{
    private const string DocsRef = "docs/self-hosting.md";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_Production_MissingPassword_IsRejected(string? password)
    {
        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.False(result.IsAccepted);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("SD_INITIAL_USER_PASSWORD", result.ErrorMessage);
        Assert.Contains(DocsRef, result.ErrorMessage);
    }

    [Theory]
    [InlineData("changeme123")]
    [InlineData("admin")]
    [InlineData("splitduo")]
    [InlineData("password")]
    [InlineData("ADMIN")]
    [InlineData("MyP4ssw0rd!")]
    public void Evaluate_Production_KnownWeakPassword_IsRejected(string password)
    {
        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.False(result.IsAccepted);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("SD_INITIAL_USER_PASSWORD", result.ErrorMessage);
    }

    // >= MinimumLength: these can only be rejected by the blocklist, not the length check.
    [Theory]
    [InlineData("changeme123456789")]
    [InlineData("qwertyqwertyqwerty")]
    [InlineData("ADMINADMINADMINX")]
    public void Evaluate_Production_LongKnownWeakPassword_IsRejectedByBlocklist(string password)
    {
        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.False(result.IsAccepted);
        Assert.Contains("SD_INITIAL_USER_PASSWORD", result.ErrorMessage);
        Assert.Contains("known-weak", result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Production_WhitespaceOnlyPassword_IsRejected()
    {
        var password = new string(' ', BootstrapCredentialPolicy.MinimumLength);

        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.False(result.IsAccepted);
        Assert.Contains("SD_INITIAL_USER_PASSWORD", result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Production_TooShortPassword_IsRejected()
    {
        var password = new string('x', BootstrapCredentialPolicy.MinimumLength - 1);

        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.False(result.IsAccepted);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Contains("SD_INITIAL_USER_PASSWORD", result.ErrorMessage);
        Assert.Contains("at least 15", result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Production_ExactlyMinimumLength_IsAccepted()
    {
        var password = new string('x', BootstrapCredentialPolicy.MinimumLength);

        var result = BootstrapCredentialPolicy.Evaluate(password, isProduction: true);

        Assert.True(result.IsAccepted);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Production_ValidPassword_IsAccepted()
    {
        var result = BootstrapCredentialPolicy.Evaluate("Xk9!mQ2vLp7wZ3nR8t", isProduction: true);

        Assert.True(result.IsAccepted);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Development_WeakPassword_IsAccepted()
    {
        var result = BootstrapCredentialPolicy.Evaluate("changeme123", isProduction: false);

        Assert.True(result.IsAccepted);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Evaluate_Development_MissingPassword_IsAccepted()
    {
        var result = BootstrapCredentialPolicy.Evaluate(null, isProduction: false);

        Assert.True(result.IsAccepted);
        Assert.Null(result.ErrorMessage);
    }
}