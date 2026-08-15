using System.Net.Http.Headers;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;

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
