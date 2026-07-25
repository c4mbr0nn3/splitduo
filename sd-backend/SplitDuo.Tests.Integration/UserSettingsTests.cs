using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;

namespace SplitDuo.Tests.Integration;

public class UserSettingsTests : IntegrationTest
{
    public UserSettingsTests(SplitDuoApiFactory factory) : base(factory) { }

    // --- GET /users/me ---

    [Fact]
    public async Task GetCurrentUser_ReturnsDefaultSettings_ForSeededAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/me", ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.NotNull(body!.Data!.Settings);
        Assert.Equal("auto", body.Data.Settings!.Theme);
        Assert.Equal("en", body.Data.Settings.UiLanguage);
    }

    // --- PUT /users/me/settings: valid updates ---

    [Fact]
    public async Task UpdateTheme_PersistsAndReturnsUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>(ct);
        Assert.Equal("dark", body!.Data!.Theme);
        Assert.Equal("en", body.Data.UiLanguage); // unchanged
    }

    [Fact]
    public async Task UpdateTheme_AppliesOnSubsequentGet()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        await client.PutAsJsonAsync("/api/v1/users/me/settings", new { theme = "light" }, ct);

        var getResponse = await client.GetAsync("/api/v1/users/me", ct);
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("light", body!.Data!.Settings!.Theme);
    }

    [Fact]
    public async Task UpdateLanguage_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "en",
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>(ct);
        Assert.Equal("en", body!.Data!.UiLanguage);
    }

    [Fact]
    public async Task UpdateBoth_PersistsBoth()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
            uiLanguage = "en",
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>(ct);
        Assert.Equal("dark", body!.Data!.Theme);
        Assert.Equal("en", body.Data.UiLanguage);
    }

    // --- PUT /users/me/settings: validation ---

    [Fact]
    public async Task UpdateTheme_InvalidValue_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "neon",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLanguage_InvalidValue_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "fr", // only "en" accepted in v1
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PUT /users/me/settings: auth ---

    [Fact]
    public async Task UpdateSettings_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        // Client has no Authorization header
        var response = await Client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/api/v1/users/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PUT /users/me/settings: null = leave unchanged ---

    [Fact]
    public async Task UpdateSettings_EmptyBody_LeavesSettingsUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // First set a known state
        await client.PutAsJsonAsync("/api/v1/users/me/settings", new { theme = "dark" }, ct);

        // Then send an empty body (both fields null)
        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new { }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>(ct);
        Assert.Equal("dark", body!.Data!.Theme); // unchanged from previous PUT
    }

    // --- jsonb round-trip (the core feature assertion) ---

    [Fact]
    public async Task Settings_RoundTripThroughJsonb_PreservesValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // Write
        await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
            uiLanguage = "en",
        }, ct);

        // Read back via a fresh request (forces DB round-trip, not in-memory cache)
        var getResponse = await client.GetAsync("/api/v1/users/me", ct);
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);

        Assert.Equal("dark", body!.Data!.Settings!.Theme);
        Assert.Equal("en", body.Data.Settings.UiLanguage);
    }
}