// SEC-02 integration tests. These run against a DEDICATED host where the REAL
// production limiter is active (the shared fixture's rate limiter is disabled —
// see the C1 block in SplitDuoApiFactory). They must never share a host with
// LockoutTests/AuthTests sequences.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Extensions;

namespace SplitDuo.Tests.Integration;

[Collection("Integration")]
public class RateLimitingTests : IntegrationTest
{
    private readonly WebApplicationFactory<Program> _rlFactory;

    public RateLimitingTests(SplitDuoApiFactory factory) : base(factory)
    {
        // Dedicated host with the real limiter: strip every
        // IConfigureOptions<RateLimiterOptions> (the app's registration AND the
        // fixture's permissive C1 twins), then re-register the production
        // policies verbatim. Trusts X-Forwarded-For so tests can vary the client IP.
        _rlFactory = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(x => x.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>)).ToList())
                services.Remove(d);
            services.AddRateLimiter(o => o.AddRateLimitingPolicies());
            services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor);
        }));
    }

    private HttpClient ClientFor(string ip)
    {
        var c = _rlFactory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return c;
    }

    private static HttpResponseMessage LoginResponse { get; }

    // The email extraction middleware binds these to the per-account bucket;
    // nonexistent emails keep tests DB-clean (401s, no lockout writes).
    [Fact]
    public async Task Login_EleventhFromSameIp_Returns429()
    {
        using var client = ClientFor("198.51.100.11");
        for (var i = 0; i < 10; i++)
            AssertLoginNot429(await SendLoginAsync(client, $"rl-ip-test-{i}@nonexistent.test"));

        var eleventh = await SendLoginAsync(client, "rl-ip-test-11@nonexistent.test");
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task Login_429HasRetryAfter60AndEmptyBody()
    {
        using var client = ClientFor("198.51.100.21");
        for (var i = 0; i < 10; i++)
            await SendLoginAsync(client, $"rl-retryafter-{i}@nonexistent.test");

        var rejected = await SendLoginAsync(client, "rl-retryafter-11@nonexistent.test");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("60", rejected.Headers.RetryAfter?.ToString());
        var bodyBytes = await rejected.Content.ReadAsByteArrayAsync();
        Assert.Empty(bodyBytes);
    }

    [Fact]
    public async Task Login_TenFromIpA_DoNotBlockIpB()
    {
        for (var i = 0; i < 10; i++)
            await SendLoginAsync(ClientFor("198.51.100.31"), $"rl-isolation-{i}@nonexistent.test");

        // A second IP is not affected by IP A's exhausted bucket
        using var ipB = ClientFor("198.51.100.32");
        var response = await SendLoginAsync(ipB, "rl-isolation-b@nonexistent.test");
        AssertLoginNot429(response);
    }

    [Fact]
    public async Task Login_ManyIpsSameEmail_16thReturns429()
    {
        const string email = "rl-account-test@nonexistent.test";
        for (var i = 0; i < 8; i++)
            AssertLoginNot429(await SendLoginAsync(ClientFor($"198.51.100.4{i}"), email));
        for (var i = 0; i < 7; i++)
            AssertLoginNot429(await SendLoginAsync(ClientFor($"198.51.100.5{i}"), email));

        // 16th request for the same account (from a different IP, under the per-IP cap)
        var sixteenth = await SendLoginAsync(ClientFor("198.51.100.60"), email);
        Assert.Equal(HttpStatusCode.TooManyRequests, sixteenth.StatusCode);
    }

    [Fact]
    public async Task Login_ManyIpsDistinctEmails_NoRateLimit()
    {
        for (var i = 0; i < 12; i++)
        {
            using var client = ClientFor($"198.51.100.7{i}");
            var response = await SendLoginAsync(client, $"rl-distinct-{i}@nonexistent.test");
            AssertLoginNot429(response);
        }
    }

    [Fact]
    public async Task Login_CaseVariantEmails_ShareAccountPartition()
    {
        for (var i = 0; i < 8; i++)
            AssertLoginNot429(await SendLoginAsync(ClientFor($"198.51.100.8{i}"), "RL-Case@nonexistent.test"));
        for (var i = 0; i < 7; i++)
            AssertLoginNot429(await SendLoginAsync(ClientFor($"198.51.100.9{i}"), "rl-case@NONEXISTENT.TEST"));

        // Case variants of the same address share one account bucket → 16th trips
        var sixteenth = await SendLoginAsync(ClientFor("198.51.100.99"), "Rl-CaSe@nonexistent.test");
        Assert.Equal(HttpStatusCode.TooManyRequests, sixteenth.StatusCode);
    }

    [Fact]
    public async Task Login_MalformedBody_StillIpLimited()
    {
        using var client = ClientFor("198.51.100.101");
        for (var i = 0; i < 10; i++)
        {
            using var content = new StringContent("not-json-at-all", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/auth/login", content);
            AssertLoginNot429(response);
        }

        using var badContent = new StringContent("not-json-at-all", Encoding.UTF8, "application/json");
        var eleventh = await client.PostAsync("/api/v1/auth/login", badContent);
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task Refresh_31stFromSameIp_Returns429()
    {
        using var client = ClientFor("198.51.100.111");
        for (var i = 0; i < 30; i++)
        {
            var response = await SendRefreshAsync(client, $"refresh-token-{i}");
            AssertLoginNot429(response);
        }

        var thirtyFirst = await SendRefreshAsync(client, "one-more-token");
        Assert.Equal(HttpStatusCode.TooManyRequests, thirtyFirst.StatusCode);
    }

    [Fact]
    public async Task Refresh_429DoesNotAffectLoginPolicy()
    {
        using var client = ClientFor("198.51.100.121");
        for (var i = 0; i < 31; i++)
            await SendRefreshAsync(client, $"refresh-token-{i}");

        // The "auth" policy is independent — login from the same IP is not rejected
        var login = await SendLoginAsync(client, "rl-refresh-login@nonexistent.test");
        AssertLoginNot429(login);
    }

    [Fact]
    public async Task ForgotPassword_11thFromSameIp_429()
    {
        using var client = ClientFor("198.51.100.131");
        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
                new { email = $"rl-forgot-{i}@nonexistent.test" });
            AssertLoginNot429(response);
        }

        var eleventh = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "rl-forgot-11@nonexistent.test" });
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task ValidateResetToken_11thFromSameIp_429()
    {
        using var client = ClientFor("198.51.100.141");
        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync(
                $"/api/v1/auth/validate-reset-token?email=rl-validate-{i}@nonexistent.test&token=0000000000000000000000000000000000000000000000000000000000000000");
            AssertLoginNot429(response);
        }

        var eleventh = await client.GetAsync(
            "/api/v1/auth/validate-reset-token?email=rl-validate-11@nonexistent.test&token=0000000000000000000000000000000000000000000000000000000000000000");
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_11thFromSameIp_429()
    {
        using var client = ClientFor("198.51.100.151");
        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
            {
                email = $"rl-reset-{i}@nonexistent.test",
                token = "0000000000000000000000000000000000000000000000000000000000000000",
                newPassword = "Not-A-Real-Password-1!",
            });
            AssertLoginNot429(response);
        }

        var eleventh = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = "rl-reset-11@nonexistent.test",
            token = "0000000000000000000000000000000000000000000000000000000000000000",
            newPassword = "Not-A-Real-Password-1!",
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_NotRateLimited()
    {
        using var client = ClientFor("198.51.100.161");
        for (var i = 0; i < 50; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string email)
    {
        using var content = JsonContent.Create(new { email, password = "definitely-not-the-password" });
        return await client.PostAsync("/api/v1/auth/login", content);
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string token)
    {
        using var content = JsonContent.Create(new { token, refreshToken = token });
        return await client.PostAsync("/api/v1/auth/refresh", content);
    }

    private static void AssertLoginNot429(HttpResponseMessage response)
        => Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
}