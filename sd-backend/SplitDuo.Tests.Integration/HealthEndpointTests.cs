using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Health;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

[Collection("Integration")]
public class HealthEndpointTests : IntegrationTest
{
    public HealthEndpointTests(SplitDuoApiFactory factory) : base(factory) { }

    [Fact]
    public void CachedHealthCheck_IsRegisteredAsSingleton()
    {
        var root = Factory.Services.GetRequiredService<CachedHealthCheck>();
        using var scope = Factory.Services.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<CachedHealthCheck>();

        Assert.Same(root, scoped);
    }

    [Fact]
    public async Task Health_ReturnsStatusOnly()
    {
        var response = await Client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.DoesNotContain("database", body);
        Assert.DoesNotContain("checks", body);
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var response = await Client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"Healthy\"}", body);
    }

    [Fact]
    public async Task HealthDetails_Anonymous_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/health/details")).StatusCode);
    }

    [Fact]
    public async Task HealthDetails_NonAdmin_Returns403()
    {
        var email = await TestDbSeeder.SeedUserAsync(Factory.Services, "health-member@localhost", "changeme123", "Health", "Member");
        using var client = await CreateAuthenticatedClientAsync(email, "changeme123");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/health/details")).StatusCode);
    }

    [Fact]
    public async Task HealthDetails_SystemAdmin_ReturnsDatabaseCheck()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/health/details");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("Healthy", json.RootElement.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
    }
}