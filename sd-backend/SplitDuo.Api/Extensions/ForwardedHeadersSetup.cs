using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace SplitDuo.Api.Extensions;

// SD_KNOWN_PROXIES: comma-separated trusted reverse proxies (plain IPs and/or
// CIDRs). Unset → forwarded headers disabled (fail-closed, stricter than the
// framework default of trusting loopback), which is safe because without
// forwarding the connection IP is the proxy itself — per-IP rate limiting then
// collapses to one bucket behind a reverse proxy.
public sealed class SplitDuoForwardedHeadersSetup(ILogger<ForwardedHeadersOptions> logger)
    : IConfigureOptions<ForwardedHeadersOptions>
{
    public void Configure(ForwardedHeadersOptions options)
    {
        var raw = Environment.GetEnvironmentVariable("SD_KNOWN_PROXIES");

        if (string.IsNullOrWhiteSpace(raw))
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            logger.LogWarning(
                "SD_KNOWN_PROXIES is not set; forwarded headers disabled — "
                + "per-IP rate limiting will collapse to one bucket behind a reverse proxy");
            return;
        }

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(entry, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
            else
            {
                try
                {
                    options.KnownIPNetworks.Add(IPNetwork.Parse(entry));
                }
                catch (FormatException)
                {
                    logger.LogWarning("SD_KNOWN_PROXIES entry '{Entry}' is neither a valid IP nor CIDR — ignored", entry);
                }
            }
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
        options.ForwardLimit = 1;
    }
}

public static class ForwardedHeadersSetupExtensions
{
    public static IServiceCollection AddSplitDuoForwardedHeaders(this IServiceCollection services)
        => services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, SplitDuoForwardedHeadersSetup>();
}