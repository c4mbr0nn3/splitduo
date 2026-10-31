using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;
using Xunit;

namespace SplitDuo.Tests.Integration;

/// <summary>
/// HTTP-level coverage for the push endpoints: authentication, the endpoint-scheme
/// check, per-user ownership, and the fact that key material never leaves the server.
/// The send path (410 purge, failure counting, address guards) is covered separately
/// by the unit tests — these exercise the API boundary the browser actually talks to.
/// </summary>
[Collection("Integration")]
public class PushEndpointsTests : IntegrationTest
{
    private const string ValidEndpoint = "https://push.example.com/subscriptions/abc123";

    /// <summary>
    /// A shape-valid VAPID public key (65-byte uncompressed P-256 point, base64url). The
    /// app now treats a malformed key as "push disabled", so the enabled cases need a real
    /// shape — an arbitrary string would return 503.
    /// </summary>
    private const string ValidVapidPublicKey =
        "BAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0-P0A";
    private const string P256dh = "BNcRd1cQw2z0test";
    private const string Auth = "tBHItJtestAuthKey";

    public PushEndpointsTests(SplitDuoApiFactory factory) : base(factory) { }

    private static object SubscriptionBody(string endpoint, string p256dh = P256dh, string auth = Auth) =>
        new { endpoint, keys = new { p256dh, auth }, userAgent = "PushEndpointsTests/1.0" };

    /// <summary>
    /// A host where push is enabled. The keys are placeholders, not valid P-256 values:
    /// enough to flip the DI branch from the no-op sender to the real one, which is all
    /// the key endpoint needs. A send-path test would need a real generated pair.
    /// </summary>
    private WebApplicationFactory<Program> WithVapidKeys() =>
        Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<PushOptions>(options =>
            {
                options.VapidPublicKey = ValidVapidPublicKey;
                options.VapidPrivateKey = "PrivateKeyForTests";
            })));

    /// <summary>A host with push explicitly disabled, regardless of the machine's env.</summary>
    private WebApplicationFactory<Program> WithoutVapidKeys() =>
        Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<PushOptions>(options =>
            {
                options.VapidPublicKey = "";
                options.VapidPrivateKey = "";
            })));

    private async Task<HttpClient> AuthenticatedClientForAsync(WebApplicationFactory<Program> factory, string email = "admin@splitduo.local")
    {
        var token = await GetAuthTokenAsync(email, "changeme123");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<List<PushSubscription>> GetRowsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PushSubscriptions.AsNoTracking()
            .OrderBy(s => s.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string endpoint) =>
        await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push/subscriptions")
        {
            Content = JsonContent.Create(new { endpoint }),
        }, TestContext.Current.CancellationToken);

    // ─── Authentication ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetVapidPublicKey_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/push/vapid-public-key", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscriptions_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/push/subscriptions", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_Unauthenticated_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/push/subscriptions", SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await GetRowsAsync());
    }

    [Fact]
    public async Task DeleteSubscription_Unauthenticated_Returns401()
    {
        var response = await DeleteAsync(Client, ValidEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Server configuration ────────────────────────────────────────────────

    [Fact]
    public async Task GetVapidPublicKey_WhenConfigured_ReturnsPublicKey()
    {
        using var web = WithVapidKeys();
        using var client = await AuthenticatedClientForAsync(web);

        var response = await client.GetAsync("/api/v1/push/vapid-public-key", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(ValidVapidPublicKey,
            body.RootElement.GetProperty("data").GetProperty("publicKey").GetString());
    }

    [Fact]
    public async Task GetVapidPublicKey_WhenNotConfigured_Returns503WithCode()
    {
        using var web = WithoutVapidKeys();
        using var client = await AuthenticatedClientForAsync(web);

        var response = await client.GetAsync("/api/v1/push/vapid-public-key", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("PUSH_NOT_CONFIGURED",
            body.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    // ─── Endpoint validation (the SSRF boundary) ─────────────────────────────

    [Theory]
    [InlineData("http://push.example.com/subscriptions/abc123")] // plaintext — blocked
    [InlineData("ftp://push.example.com/subscriptions/abc123")] // wrong scheme
    [InlineData("push.example.com/subscriptions/abc123")] // not absolute
    [InlineData("not a url at all")]
    public async Task CreateSubscription_NonHttpsOrMalformedEndpoint_Returns400(string endpoint)
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/push/subscriptions", SubscriptionBody(endpoint), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetRowsAsync());
    }

    // ─── Create / upsert ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubscription_HttpsEndpoint_StoresOneRowForCaller()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var adminId = await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local");

        var response = await client.PostAsJsonAsync(
            "/api/v1/push/subscriptions", SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await GetRowsAsync());
        Assert.Equal(ValidEndpoint, row.Endpoint);
        Assert.Equal(adminId, row.UserId);
        Assert.Equal(0, row.FailureCount);
    }

    [Fact]
    public async Task CreateSubscription_SameEndpointTwice_UpdatesInsteadOfDuplicating()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var first = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint, p256dh: "BUpdatedKey", auth: Auth), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var row = Assert.Single(await GetRowsAsync());
        Assert.Equal("BUpdatedKey", row.P256dh);
    }

    [Fact]
    public async Task CreateSubscription_EndpointOwnedByAnotherUser_Returns409()
    {
        using var adminClient = await CreateAuthenticatedClientAsync();
        var created = await adminClient.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await TestDbSeeder.SeedUserAsync(Factory.Services, "push2@localhost", "changeme123");
        using var otherClient = await CreateAuthenticatedClientAsync("push2@localhost");

        var response = await otherClient.PostAsJsonAsync(
            "/api/v1/push/subscriptions", SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var row = Assert.Single(await GetRowsAsync());
        Assert.Equal(
            await TestDbSeeder.GetUserIntIdAsync(Factory.Services, "admin@splitduo.local"),
            row.UserId); // still owned by the original registrant
        // A regression that updates keys before returning Conflict would point the
        // victim's row at the attacker's keypair — silent loss of their notifications.
        Assert.Equal(P256dh, row.P256dh);
    }

    [Fact]
    public async Task CreateSubscription_BeyondPerUserCap_Returns400AndKeepsExistingDevices()
    {
        using var client = await CreateAuthenticatedClientAsync();

        for (var i = 0; i < 10; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
                SubscriptionBody($"https://push.example.com/subscriptions/device-{i}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var overLimit = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody("https://push.example.com/subscriptions/device-10"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);

        var rows = await GetRowsAsync();
        Assert.Equal(10, rows.Count);
        Assert.DoesNotContain(rows, r => r.Endpoint.EndsWith("device-10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateSubscription_AtCap_UpdatingExistingDeviceStillSucceeds()
    {
        using var client = await CreateAuthenticatedClientAsync();

        for (var i = 0; i < 10; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
                SubscriptionBody($"https://push.example.com/subscriptions/device-{i}"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        // A rotated browser subscription re-registers an endpoint the user already owns.
        var update = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody("https://push.example.com/subscriptions/device-0", p256dh: "BRotatedKey"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var rows = await GetRowsAsync();
        Assert.Equal(10, rows.Count);
        Assert.Equal("BRotatedKey",
            Assert.Single(rows, r => r.Endpoint.EndsWith("device-0", StringComparison.Ordinal)).P256dh);
    }

    // ─── List ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubscriptions_ReturnsOnlyCallerRows_AndNoKeyMaterial()
    {
        using var adminClient = await CreateAuthenticatedClientAsync();
        var adminPost = await adminClient.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, adminPost.StatusCode);

        await TestDbSeeder.SeedUserAsync(Factory.Services, "push3@localhost", "changeme123");
        using var otherClient = await CreateAuthenticatedClientAsync("push3@localhost");
        const string otherEndpoint = "https://push.example.com/subscriptions/other";
        var otherPost = await otherClient.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(otherEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, otherPost.StatusCode);
        Assert.Equal(2, (await GetRowsAsync()).Count);

        var raw = await adminClient.GetStringAsync("/api/v1/push/subscriptions", TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(raw);
        var rows = body.RootElement.GetProperty("data").EnumerateArray().ToList();
        var only = Assert.Single(rows);
        Assert.Equal(ValidEndpoint, only.GetProperty("endpoint").GetString());

        // Key material is server-side only: the browser never needs it back.
        Assert.DoesNotContain(P256dh, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(Auth, raw, StringComparison.Ordinal);

        // Mirror check: the property is "only my rows", not "one row".
        var otherRaw = await otherClient.GetStringAsync("/api/v1/push/subscriptions", TestContext.Current.CancellationToken);
        using var otherBody = JsonDocument.Parse(otherRaw);
        var otherOnly = Assert.Single(otherBody.RootElement.GetProperty("data").EnumerateArray().ToList());
        Assert.Equal(otherEndpoint, otherOnly.GetProperty("endpoint").GetString());
    }

    // ─── Delete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSubscription_OwnEndpoint_RemovesRow()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var created = await client.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var response = await DeleteAsync(client, ValidEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await GetRowsAsync());
    }

    [Fact]
    public async Task DeleteSubscription_OtherUsersEndpoint_Returns404AndKeepsRow()
    {
        using var adminClient = await CreateAuthenticatedClientAsync();
        var created = await adminClient.PostAsJsonAsync("/api/v1/push/subscriptions",
            SubscriptionBody(ValidEndpoint), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await TestDbSeeder.SeedUserAsync(Factory.Services, "push4@localhost", "changeme123");
        using var otherClient = await CreateAuthenticatedClientAsync("push4@localhost");

        var response = await DeleteAsync(otherClient, ValidEndpoint);

        // 404 rather than 200/403: an attacker cannot confirm another user's endpoint exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await GetRowsAsync());
    }
}
