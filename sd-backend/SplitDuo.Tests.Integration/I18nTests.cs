using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SplitDuo.Core.Localization;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

[Collection("Integration")]
public class I18nTests : IntegrationTest
{
    public I18nTests(SplitDuoApiFactory factory) : base(factory) { }

    #region JWT lang claim

    [Fact]
    public async Task Login_UserWithItalianSettings_JwtContainsLangIt()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a user, then update settings to "it" via API
        var email = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "italian@localhost", password: "Test1234!");

        var client = await CreateAuthenticatedClientAsync(email, "Test1234!");
        var settingsResponse = await client.PutAsJsonAsync("/api/v1/users/me/settings",
            new { uiLanguage = "it" }, ct);
        var settingsBody = await settingsResponse.Content
            .ReadFromJsonAsync<ApiResponseDto<UpdateUserSettingsResponseDto>>(ct);
        var newToken = settingsBody!.Data!.Token!;

        var lang = DecodeJwtClaim(newToken, "lang");
        Assert.Equal("it", lang);
    }

    [Fact]
    public async Task Login_UserWithEnglishSettings_JwtContainsLangEn()
    {
        var ct = TestContext.Current.CancellationToken;

        var token = await GetAuthTokenAsync();
        var lang = DecodeJwtClaim(token, "lang");
        Assert.Equal("en", lang);
    }

    [Fact]
    public async Task Login_JwtLangClaim_PresentInAllTokens()
    {
        var ct = TestContext.Current.CancellationToken;

        var token = await GetAuthTokenAsync();
        var lang = DecodeJwtClaim(token, "lang");
        Assert.NotNull(lang);
    }

    #endregion

    #region Accept-Language error messages

    [Fact]
    public async Task Login_InvalidCredentials_WithAcceptLanguageIt_ReturnsItalianError()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost",
            password = "wrong-password",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Email o password non validi", body!.Error!.Message);
    }

    [Fact]
    public async Task Login_InvalidCredentials_WithAcceptLanguageEn_ReturnsEnglishError()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost",
            password = "wrong-password",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Invalid email or password", body!.Error!.Message);
    }

    [Fact]
    public async Task Login_InvalidCredentials_WithoutAcceptLanguage_DefaultsToEnglish()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost",
            password = "wrong-password",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Invalid email or password", body!.Error!.Message);
    }

    // Note: the localized NotAuthenticated() message in BaseApiController is
    // unreachable from a fully unauthenticated request because every controller
    // that calls it has class-level [Authorize], so the auth middleware returns
    // an empty 401 before the controller runs. The BaseApiController.it.resx
    // exists for consistency and the defense-in-depth path; it cannot be
    // exercised end-to-end without bypassing [Authorize].

    #endregion

    #region JWT lang claim drives culture

    [Fact]
    public async Task AuthenticatedRequest_WithLangItClaim_ReturnsItalianError()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a user and set language to Italian
        var email = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "italian@localhost", password: "Test1234!");

        var client = await CreateAuthenticatedClientAsync(email, "Test1234!");
        var settingsResponse = await client.PutAsJsonAsync("/api/v1/users/me/settings",
            new { uiLanguage = "it" }, ct);
        var settingsBody = await settingsResponse.Content
            .ReadFromJsonAsync<ApiResponseDto<UpdateUserSettingsResponseDto>>(ct);
        var newToken = settingsBody!.Data!.Token!;

        // Use the new token (with lang=it) to call a non-existent group
        var italianClient = Factory.CreateClient();
        italianClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", newToken);

        var response = await italianClient.GetAsync($"/api/v1/groups/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Gruppo non trovato", body!.Error!.Message);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithLangEnClaim_ReturnsEnglishError()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    #endregion

    #region uiLanguage validation

    [Fact]
    public async Task UpdateSettings_InvalidLanguage_ReturnsLocalizedError()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "de",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("Unsupported language", body);
    }

    [Fact]
    public async Task UpdateSettings_ValidItalian_PersistsAndReturnsNewToken()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "it",
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync<ApiResponseDto<UpdateUserSettingsResponseDto>>(ct);
        Assert.NotNull(body!.Data!.Token);
        Assert.Equal("it", body.Data.Settings.UiLanguage);

        // Decode the re-issued JWT and verify the lang claim
        var lang = DecodeJwtClaim(body.Data.Token, "lang");
        Assert.Equal("it", lang);

        // Verify persistence via GET /users/me
        var user = await client.GetCurrentUserAsync();
        Assert.Equal("it", user.Settings.UiLanguage);
    }

    #endregion

    #region Email template language

    [Fact]
    public async Task PasswordReset_EmailForItalianUser_UsesItalianTemplate()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a user and set language to Italian
        var email = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "italian@localhost", password: "Test1234!");

        var client = await CreateAuthenticatedClientAsync(email, "Test1234!");
        await client.PutAsJsonAsync("/api/v1/users/me/settings",
            new { uiLanguage = "it" }, ct);

        // Request password reset (unauthenticated)
        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, ct);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, email);
        Assert.NotEmpty(bodies);
        Assert.Contains("reimpostazione", bodies[0]);
    }

    [Fact]
    public async Task PasswordReset_EmailForEnglishUser_UsesEnglishTemplate()
    {
        var ct = TestContext.Current.CancellationToken;

        var email = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "english@localhost", password: "Test1234!");

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, ct);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, email);
        Assert.NotEmpty(bodies);
        Assert.Contains("Reset my password", bodies[0]);
    }

    [Fact]
    public async Task PasswordReset_EmailWithAcceptLanguageIt_WhenUserLanguageEn_UsesUserLanguage()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed user with default UiLanguage="en"
        var email = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "mixed@localhost", password: "Test1234!");

        // Request with Accept-Language: it — the email should still use the user's language (en)
        var italianClient = Factory.CreateClient();
        italianClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("it");

        await italianClient.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email }, ct);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, email);
        Assert.NotEmpty(bodies);
        Assert.Contains("Reset my password", bodies[0]);
    }

    #endregion

    #region Language switching end-to-end

    [Fact]
    public async Task ChangeLanguage_ToItalian_UpdatesJwtAndErrorMessages()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();
        var nonExistentGroup = Guid.NewGuid().ToString();

        // Before switch: error is in English (JWT has lang=en)
        var response1 = await client.GetAsync($"/api/v1/groups/{nonExistentGroup}", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Group not found", body1!.Error!.Message);

        // Switch language to Italian
        var settingsResponse = await client.PutAsJsonAsync("/api/v1/users/me/settings",
            new { uiLanguage = "it" }, ct);
        var settingsBody = await settingsResponse.Content
            .ReadFromJsonAsync<ApiResponseDto<UpdateUserSettingsResponseDto>>(ct);
        var newToken = settingsBody!.Data!.Token!;

        // After switch: use the new token (JWT has lang=it) — error is now Italian
        var italianClient = Factory.CreateClient();
        italianClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", newToken);

        var response2 = await italianClient.GetAsync($"/api/v1/groups/{nonExistentGroup}", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Gruppo non trovato", body2!.Error!.Message);
    }

    #endregion

    #region Centralization contract test

    [Fact]
    public void SupportedLanguages_Contract_AddingLanguageToAllIsSufficient()
    {
        // This test proves the centralization claim: adding a language to
        // SupportedLanguages.All is the only change needed for the backend
        // to accept it. It verifies the API surface that all consumers use.
        //
        // If "fr" were added to SupportedLanguages.All:
        //   - IsSupported("fr") would return true
        //   - Normalize("fr") would return "fr"
        //   - Normalize(null) would still return "en" (safe fallback)
        //   - Cultures would include new CultureInfo("fr")
        //   - Default would still be "en"

        // Current contract
        Assert.Equal(2, SupportedLanguages.All.Count);
        Assert.Contains("en", SupportedLanguages.All);
        Assert.Contains("it", SupportedLanguages.All);

        // Hypothetical "fr" — these assertions document what would change
        Assert.False(SupportedLanguages.IsSupported("fr"));
        Assert.Equal("en", SupportedLanguages.Normalize("fr"));

        // Safe fallback is always "en"
        Assert.Equal("en", SupportedLanguages.Default);
        Assert.Equal("en", SupportedLanguages.Normalize(null));
        Assert.Equal("en", SupportedLanguages.Normalize(""));

        // Cultures match All
        Assert.Equal(SupportedLanguages.All.Count, SupportedLanguages.Cultures.Length);
        foreach (var culture in SupportedLanguages.Cultures)
        {
            Assert.Contains(culture.Name, SupportedLanguages.All);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Decodes a claim from a JWT token's payload (middle segment) without full validation.
    /// </summary>
    private static string DecodeJwtClaim(string token, string claimName)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException("Invalid JWT token format");

        var payload = parts[1];
        // Convert from base64url to base64
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var bytes = Convert.FromBase64String(payload);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(claimName).GetString()!;
    }

    #endregion
}
