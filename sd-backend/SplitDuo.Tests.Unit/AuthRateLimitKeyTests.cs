using System.Net;
using System.Security.Cryptography;
using System.Text;
using SplitDuo.Api.Extensions;
using Xunit;

namespace SplitDuo.Tests.Unit;

// Unit tests for the pure partition-key helpers behind SEC-02 auth rate limiting.
public class AuthRateLimitKeyTests
{
    [Fact]
    public void NormalizeIpKey_Ipv4_PassesThrough()
    {
        var result = AuthRateLimiting.NormalizeIpKey(IPAddress.Parse("203.0.113.7"));
        Assert.Equal("203.0.113.7", result);
    }

    [Fact]
    public void NormalizeIpKey_Ipv6_ReturnsSlash64Prefix()
    {
        var ip = IPAddress.Parse("2001:db8:1234:5678:9abc:def0:1122:3344");
        var result = AuthRateLimiting.NormalizeIpKey(ip);
        // /64 network prefix: last 8 octets zeroed
        Assert.Equal("2001:db8:1234:5678::", result);
    }

    [Fact]
    public void NormalizeIpKey_Ipv4MappedIpv6_MapsBackToIpv4()
    {
        var ip = IPAddress.Parse("::ffff:1.2.3.4");
        var result = AuthRateLimiting.NormalizeIpKey(ip);
        Assert.Equal("1.2.3.4", result);
    }

    [Fact]
    public void NormalizeIpKey_Null_ReturnsUnknown()
    {
        Assert.Equal("unknown", AuthRateLimiting.NormalizeIpKey(null));
    }

    [Fact]
    public void NormalizeIpKey_Ipv4MappedIpv6_DifferentHostBitsSameBucket()
    {
        // Two distinct addresses inside the same /64 must share a bucket
        Assert.Equal(
            AuthRateLimiting.NormalizeIpKey(IPAddress.Parse("2001:db8::1")),
            AuthRateLimiting.NormalizeIpKey(IPAddress.Parse("2001:db8::ffff")));
    }

    [Fact]
    public void NormalizeAccountKey_TrimsAndLowercases()
    {
        Assert.Equal("user@example.com", AuthRateLimiting.NormalizeAccountKey("  User@Example.COM "));
    }

    [Fact]
    public void NormalizeAccountKey_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(AuthRateLimiting.NormalizeAccountKey(null));
        Assert.Null(AuthRateLimiting.NormalizeAccountKey(""));
        Assert.Null(AuthRateLimiting.NormalizeAccountKey("   "));
    }

    [Fact]
    public void NormalizeAccountKey_LongEmail_DeterministicHash()
    {
        var longEmail = new string('a', 300) + "@example.com";
        var first = AuthRateLimiting.NormalizeAccountKey(longEmail);
        var second = AuthRateLimiting.NormalizeAccountKey("  " + longEmail + " ");

        Assert.NotNull(first);
        Assert.StartsWith("long:", first);
        Assert.Equal(first, second);

        // Matches the documented SHA256 hex of the lowercased/trimmed value
        var data = Encoding.UTF8.GetBytes(longEmail.Trim().ToLowerInvariant());
        var expected = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        Assert.Equal("long:" + expected, first);
    }

    [Fact]
    public void NormalizeAccountKey_ExactlyAtLimitNotHashed()
    {
        var email = new string('a', 244) + "@example.com"; // 256 chars
        Assert.False(AuthRateLimiting.NormalizeAccountKey(email)!.StartsWith("long:"));
    }
}