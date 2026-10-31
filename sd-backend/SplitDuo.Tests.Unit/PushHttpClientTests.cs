using System.Net;
using SplitDuo.Core.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

/// <summary>
/// Guards the address classification behind the push SSRF hardening. A regression here
/// silently re-opens the hole: the endpoint check only enforces `https`, so these
/// ranges are the last line of defence before the server connects.
/// </summary>
public class PushHttpClientTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("172.32.0.1", true)] // just outside RFC 1918 172.16/12
    [InlineData("100.128.0.1", true)] // just outside CGNAT
    [InlineData("192.169.0.1", true)] // just outside 192.168/16
    [InlineData("2606:4700:4700::1111", true)] // public IPv6
    [InlineData("172.15.255.255", true)] // just below RFC 1918 172.16/12
    [InlineData("2002:0808:0808::", true)] // 6to4 embedding 8.8.8.8
    [InlineData("64:ff9b::808:808", true)] // NAT64 embedding 8.8.8.8
    public void IsPublicAddress_AllowsInternetAddresses(string address, bool expected)
    {
        Assert.Equal(expected, PushHttpClient.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("127.1.2.3")] // any 127/8
    [InlineData("0.0.0.0")] // this network
    [InlineData("10.1.2.3")] // RFC 1918
    [InlineData("172.16.0.1")] // RFC 1918 lower bound
    [InlineData("172.31.255.255")] // RFC 1918 upper bound
    [InlineData("192.168.1.10")] // RFC 1918
    [InlineData("169.254.169.254")] // link-local / cloud metadata
    [InlineData("100.64.0.1")] // CGNAT lower bound
    [InlineData("100.127.255.255")] // CGNAT upper bound
    [InlineData("224.0.0.1")] // multicast
    [InlineData("255.255.255.255")] // broadcast
    [InlineData("::1")] // IPv6 loopback
    [InlineData("::")] // unspecified
    [InlineData("fe80::1")] // IPv6 link-local
    [InlineData("fc00::1")] // IPv6 unique-local lower bound
    [InlineData("fdff::1")] // IPv6 unique-local upper bound
    [InlineData("fec0::1")] // IPv6 site-local
    [InlineData("fe80::1%1")] // link-local with zone id
    [InlineData("::127.0.0.1")] // IPv4-compatible loopback
    [InlineData("::169.254.169.254")] // IPv4-compatible link-local
    [InlineData("64:ff9b::c0a8:101")] // NAT64 embedding 192.168.1.1
    [InlineData("64:ff9b:1::a9fe:a9fe")] // local-use NAT64 embedding 169.254.169.254
    [InlineData("2002:c0a8:101::")] // 6to4 embedding 192.168.1.1
    [InlineData("2002:7f00:1::")] // 6to4 embedding 127.0.0.1
    public void IsPublicAddress_RejectsInternalAddresses(string address)
    {
        Assert.False(PushHttpClient.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:192.168.1.1")]
    public void IsPublicAddress_RejectsIpv4MappedIpv6OfInternalAddress(string address)
    {
        // The MapToIPv4 unwrapping must cover every blocked range, not just RFC 1918.
        Assert.False(PushHttpClient.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Fact]
    public void Handler_DoesNotFollowRedirects()
    {
        // A public https endpoint the attacker controls could redirect to an internal
        // address; following it would undo the scheme check at registration.
        using var handler = PushHttpClient.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
    }
}
