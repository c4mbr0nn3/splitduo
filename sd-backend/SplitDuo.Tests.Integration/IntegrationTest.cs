using System.Net.Http.Headers;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<SplitDuoApiFactory> { }

[Collection("Integration")]
public abstract class IntegrationTest : IAsyncLifetime
{
    protected readonly SplitDuoApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTest(SplitDuoApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });
    }

    public virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public virtual async ValueTask DisposeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    /// <summary>
    /// Seeds a second user, adds them to the group as a member, and returns an
    /// authenticated client for them.
    /// </summary>
    protected async Task<(string email, string userId, HttpClient client)> SeedSecondMemberAsync(
        HttpClient adminClient, string groupId, string role = "member",
        string email = "user2@localhost")
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email, "changeme123", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/members", new { userEmail = memberEmail, role }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();
        return (memberEmail, member.Id, memberClient);
    }

    /// <summary>
    /// Seeds a two-member group (admin + u2@localhost) and returns
    /// (adminClient, groupId, adminId, user2Id).
    /// </summary>
    protected async Task<(HttpClient adminClient, string groupId, string adminId, string user2Id)>
        SetupGroupWithTwoMembersAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "u2@localhost", "changeme123", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await memberClient.GetCurrentUserAsync();

        return (adminClient, group.Id, admin.Id, user2.Id);
    }

    /// <summary>
    /// Logs in via the real /auth/login endpoint and returns the JWT.
    /// Rate limiter is disabled in the test host, so repeated logins are safe.
    /// </summary>
    protected async Task<string> GetAuthTokenAsync(
        string email = "admin@splitduo.local",
        string password = "changeme123")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>();
        return body!.Data!.Token;
    }

    /// <summary>
    /// Returns an HttpClient with a valid Bearer token set.
    /// </summary>
    protected async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "admin@splitduo.local",
        string password = "changeme123")
    {
        var token = await GetAuthTokenAsync(email, password);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
