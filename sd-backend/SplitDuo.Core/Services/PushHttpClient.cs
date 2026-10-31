using System.Net;
using System.Net.Sockets;

namespace SplitDuo.Core.Services;

/// <summary>
/// The single <see cref="HttpClient"/> every push send goes through.
///
/// The destination comes from a row the client wrote (the subscription endpoint),
/// which would otherwise make this feature a request-forgery primitive: the server
/// can be told to POST to an address of the attacker's choosing, from inside the
/// network it runs in. The registration check only enforces `https`; this class
/// closes the two ways around it:
///
/// - <b>Redirects</b> are not followed. A public https endpoint the attacker controls
///   could otherwise answer 302 and send the request on to http://169.254.169.254/.
/// - <b>Non-public addresses</b> are refused at connect time, after DNS resolution,
///   so neither a literal https://192.168.1.10/ nor a public hostname that resolves
///   internally can be reached. Resolution happens per connection
///   (PooledConnectionLifetime bounds staleness), so a DNS answer cannot be pinned.
///
/// A refusal surfaces as an ordinary send failure, which the failure counter handles.
/// </summary>
public sealed class PushHttpClient : IDisposable
{
    public PushHttpClient() : this(CreateHandler())
    {
    }

    /// <summary>
    /// Test seam: lets the send-path tests supply a handler that answers offline. The
    /// handler bypasses the transport guards entirely, so it must never be used outside
    /// tests — those guards are covered by <c>PushHttpClientTests</c>.
    /// The client owns the handler (disposeHandler: true); the production path chains
    /// through here, so the guarded handler is disposed exactly like before.
    /// </summary>
    internal PushHttpClient(HttpMessageHandler handler)
    {
        Client = new HttpClient(handler, disposeHandler: true);
    }

    public HttpClient Client { get; }

    public void Dispose() => Client.Dispose();

    internal static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        // Bound how long a resolved address may be reused, so a hostname cannot be
        // pointed at an internal address and kept alive through a pooled connection.
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectCallback = ConnectToPublicAddressAsync,
    };

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;

        // A literal address never goes through DNS; check it directly.
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        var target = Array.Find(addresses, IsPublicAddress)
                     ?? throw new HttpRequestException(
                         $"Refusing to connect to '{host}': it resolves only to loopback, private or link-local addresses.");

        var socket = new Socket(target.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(target, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// True only for addresses that are routable on the public internet. Everything the
    /// server could reach but a remote user could not is rejected: loopback, RFC 1918,
    /// CGNAT (Tailscale and some ISPs), link-local (cloud metadata lives here),
    /// IPv6 unique-local, multicast and reserved space.
    /// </summary>
    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsPublicAddress(address.MapToIPv4());

        if (IPAddress.IsLoopback(address))
            return false;

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsPublicIPv4(address);

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return false;

            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)
                return false; // fc00::/7 unique-local

            // Some IPv6 forms carry an IPv4 address inside them. A NAT64 or 6to4
            // gateway translates that embedded address, so 64:ff9b::a9fe:a9fe is
            // 169.254.169.254 in disguise. Classify the address they actually reach.
            return !TryGetEmbeddedIPv4(b, out var embedded) || IsPublicIPv4(embedded);
        }

        return false;
    }

    private static bool IsPublicIPv4(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] switch
        {
            0 => false, // "this network"
            10 => false, // RFC 1918
            100 when b[1] is >= 64 and <= 127 => false, // CGNAT
            127 => false, // loopback
            169 when b[1] == 254 => false, // link-local / cloud metadata
            172 when b[1] is >= 16 and <= 31 => false, // RFC 1918
            192 when b[1] == 168 => false, // RFC 1918
            >= 224 => false, // multicast, reserved, broadcast
            _ => true,
        };
    }

    /// <summary>
    /// Extracts the IPv4 address embedded in an IPv4-compatible, NAT64 or 6to4 IPv6
    /// address. Returns false for ordinary IPv6 addresses, which carry no IPv4 target.
    /// </summary>
    private static bool TryGetEmbeddedIPv4(byte[] b, out IPAddress embedded)
    {
        embedded = IPAddress.None;

        var lastEightZero =
            b[6] == 0 && b[7] == 0 && b[8] == 0 && b[9] == 0 && b[10] == 0 && b[11] == 0;

        // ::/96 — IPv4-compatible (::1 and :: are rejected earlier).
        if (b[0] == 0 && b[1] == 0 && b[2] == 0 && b[3] == 0 && b[4] == 0 && b[5] == 0 && lastEightZero)
        {
            embedded = new IPAddress(b[12..16]);
            return true;
        }

        // 64:ff9b::/96 — NAT64 well-known prefix.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B
            && b[4] == 0 && b[5] == 0 && lastEightZero)
        {
            embedded = new IPAddress(b[12..16]);
            return true;
        }

        // 64:ff9b:1::/48 — RFC 8215 local-use NAT64, defined to reach private IPv4.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B
            && b[4] == 0x00 && b[5] == 0x01 && lastEightZero)
        {
            embedded = new IPAddress(b[12..16]);
            return true;
        }

        // 2002::/16 — 6to4, embeds the IPv4 address right after the prefix.
        if (b[0] == 0x20 && b[1] == 0x02)
        {
            embedded = new IPAddress([b[2], b[3], b[4], b[5]]);
            return true;
        }

        return false;
    }
}
