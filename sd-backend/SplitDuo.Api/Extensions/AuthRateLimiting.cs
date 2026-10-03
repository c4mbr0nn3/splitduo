using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SplitDuo.Api.Extensions;

// SEC-02: two-dimensional rate limiting for auth endpoints.
// Named policies key on client IP; the GlobalLimiter keys on the (normalized)
// account email extracted by UseAuthEmailExtraction. Requests without an
// extracted email get NoLimiter from the global limiter, so only the per-IP
// dimension applies to non-auth, static, and health requests.
public static class AuthRateLimiting
{
    public const string AuthPolicyName = "auth";
    public const string AuthRefreshPolicyName = "auth-refresh";
    internal const string EmailItemsKey = "SplitDuo.AuthRateLimiting.Email";

    public static RateLimiterOptions AddRateLimitingPolicies(this RateLimiterOptions options)
    {
        options.AddPolicy(AuthPolicyName, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                "ip:" + NormalizeIpKey(httpContext.Connection.RemoteIpAddress),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 3,
                    QueueLimit = 0,
                }));

        options.AddPolicy(AuthRefreshPolicyName, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                "ip:" + NormalizeIpKey(httpContext.Connection.RemoteIpAddress),
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 3,
                    QueueLimit = 0,
                }));

        options.AddPolicy("receipt-scan", httpContext =>
        {
            var partitionKey = httpContext.User.FindFirst("userId")?.Value
                               ?? httpContext.Connection.RemoteIpAddress?.ToString()
                               ?? "anon";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        });

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            if (ctx.Items[EmailItemsKey] is not string accountKey)
                return RateLimitPartition.GetNoLimiter("no-email");

            // 15 per 15 minutes = 60/hour (NIST SP 800-63B §5.2.2 allows ≤ 100)
            return RateLimitPartition.GetSlidingWindowLimiter(
                "acct:" + accountKey,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromMinutes(15),
                    SegmentsPerWindow = 3,
                    QueueLimit = 0,
                });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // RFC 6585 §4: Retry-After hints when to retry; RFC 9110 §10.2.3 for field semantics.
        // Status code is set explicitly here (OnRejected bypasses RejectionStatusCode).
        options.OnRejected = (ctx, _) =>
        {
            ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ctx.HttpContext.Response.Headers.RetryAfter = "60";
            return ValueTask.CompletedTask;
        };

        return options;
    }

    /// <summary>
    /// Extracts the account email from the request (JSON body for POST, query for
    /// GET) and stores the normalized key in HttpContext.Items so the auth
    /// GlobalLimiter can partition per account. Any failure leaves no key — the
    /// per-IP dimension still applies.
    /// </summary>
    public static IApplicationBuilder UseAuthEmailExtraction(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            var buffered = false;
            try
            {
                if (ctx.Request.Path.StartsWithSegments("/api/v1/auth"))
                {
                    if (HttpMethods.IsGet(ctx.Request.Method))
                    {
                        var email = ctx.Request.Query["email"];
                        if (email.Count > 0)
                            ctx.Items[EmailItemsKey] = NormalizeAccountKey(email.ToString());
                    }
                    else if (HttpMethods.IsPost(ctx.Request.Method)
                             && ctx.Request.ContentLength is > 0 and <= 16384
                             && IsJsonContentType(ctx.Request.ContentType))
                    {
                        ctx.Request.EnableBuffering();
                        buffered = true;
                        using var reader = new StreamReader(
                            ctx.Request.Body, Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: false,
                            bufferSize: 1024, leaveOpen: true);
                        var body = await reader.ReadToEndAsync();
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("email", out var el)
                            && el.ValueKind == JsonValueKind.String)
                        {
                            ctx.Items[EmailItemsKey] = NormalizeAccountKey(el.GetString());
                        }
                    }
                }
            }
            catch
            {
                // Malformed body / oversized / parse failure: no account key —
                // the per-IP dimension keeps the request bounded.
                ctx.Items.Remove(EmailItemsKey);
            }
            finally
            {
                // MVC model binding must be able to re-read the body. Only seek
                // when EnableBuffering wrapped the stream — the raw request body
                // stream is not seekable.
                if (buffered)
                    ctx.Request.Body.Seek(0, SeekOrigin.Begin);
            }

            await next(ctx);
        });

    /// <summary>
    /// Builds a stable per-IP partition key: IPv4 is used as-is; IPv4-mapped
    /// IPv6 is mapped back to IPv4; plain IPv6 is normalized to its /64 network
    /// prefix (individual addresses within one /64 share a bucket).
    /// </summary>
    public static string NormalizeIpKey(IPAddress? remoteIp)
    {
        if (remoteIp is null)
            return "unknown";

        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        if (remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return remoteIp.ToString();

        // IPv6: key on the /64 network prefix, not the full address.
        var bytes = remoteIp.GetAddressBytes();
        if (bytes.Length == 16)
            Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Builds a bounded, deterministic per-account partition key: lowercase,
    /// trimmed; inputs longer than 256 chars are hashed (same input → same key).
    /// </summary>
    public static string? NormalizeAccountKey(string? rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
            return null;

        var normalized = rawEmail.Trim().ToLowerInvariant();
        if (normalized.Length > 256)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return "long:" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        return normalized;
    }

    private static bool IsJsonContentType(string? contentType) =>
        contentType is not null
        && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
}