using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class AuthTests : IntegrationTest
{
    public AuthTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Login — happy path

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndUser()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost",
            password = "changeme",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.False(body.Data!.RequiresTwoFactor);
        Assert.NotEmpty(body.Data.Token);
        Assert.NotEmpty(body.Data.RefreshToken);
        Assert.True(body.Data.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal("admin@localhost", body.Data.User.Email);
        Assert.NotEmpty(body.Data.User.Id);
    }

    [Fact]
    public async Task Login_NewLoginIssuesIndependentRefreshTokenFamily()
    {
        var ct = TestContext.Current.CancellationToken;

        // First login
        var first = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var firstBody = await first.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var firstRefresh = firstBody!.Data!.RefreshToken;

        // Second login (same user) — issues a new refresh token in a separate family.
        // The first refresh token remains valid (multi-device support); it is NOT revoked.
        var second = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotEqual(firstRefresh, secondBody!.Data!.RefreshToken);

        // First refresh token still works (independent family, not revoked by new login)
        var refreshResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = firstBody.Data!.Token,
            refreshToken = firstRefresh,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    #endregion

    #region Login — failure

    [Fact]
    public async Task Login_WrongPassword_Returns401()
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

    [Fact]
    public async Task Login_UnknownEmail_Returns401_SameMessageAsWrongPassword()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "nobody@nowhere.test",
            password = "whatever-123",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        // Same message as wrong-password — prevents email enumeration
        Assert.Equal("Invalid email or password", body!.Error!.Message);
    }

    [Fact]
    public async Task Login_MissingEmail_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            password = "changeme",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_PasswordTooShort_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost",
            password = "short",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Refresh — rotation + reuse detection

    [Fact]
    public async Task Refresh_ValidTokens_ReturnsNewTokenPair()
    {
        var ct = TestContext.Current.CancellationToken;

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var oldToken = loginBody!.Data!.Token;
        var oldRefresh = loginBody.Data.RefreshToken;

        var refreshResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = oldToken,
            refreshToken = oldRefresh,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotEmpty(refreshBody!.Data!.Token);
        Assert.NotEmpty(refreshBody.Data.RefreshToken);
        Assert.NotEqual(oldToken, refreshBody.Data.Token);
        Assert.NotEqual(oldRefresh, refreshBody.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusingOldRefreshTokenWithinGraceWindow_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;

        // Login → refresh once → reuse the original (now-revoked "Used for refresh") refresh token.
        // The auth service grants a 30-second grace window for "Used for refresh" tokens to tolerate
        // network retries / lost responses, so a reuse within that window is accepted.
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var oldToken = loginBody!.Data!.Token;
        var oldRefresh = loginBody.Data.RefreshToken;

        // First refresh succeeds and rotates
        var firstRefresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = oldToken, refreshToken = oldRefresh,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        // Reuse the original (revoked "Used for refresh") refresh token within the grace window
        var reuseResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = oldToken, refreshToken = oldRefresh,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, reuseResponse.StatusCode);
        var reuseBody = await reuseResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotEmpty(reuseBody!.Data!.Token);
        Assert.NotEmpty(reuseBody.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ReusingRevokedForOtherReason_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        // Login → explicitly revoke the refresh token (reason "User logout") → try to refresh with it.
        // Tokens revoked for reasons other than "Used for refresh" have no grace window.
        var client = await CreateAuthenticatedClientAsync();
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var oldToken = loginBody!.Data!.Token;
        var oldRefresh = loginBody.Data.RefreshToken;

        // Explicit revoke (reason "User logout")
        await client.PostAsJsonAsync("/api/v1/auth/revoke", new { refreshToken = oldRefresh }, ct);

        // Reuse of the explicitly-revoked token is rejected (no grace window for "User logout")
        var reuseResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = oldToken, refreshToken = oldRefresh,
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        var reuseBody = await reuseResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Refresh token is no longer valid", reuseBody!.Error!.Message);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);

        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = loginBody!.Data!.Token,
            refreshToken = "not-a-real-token",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Invalid refresh token", body!.Error!.Message);
    }

    [Fact]
    public async Task Refresh_GarbageJwt_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = "garbage.jwt.payload",
            refreshToken = "also-garbage",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Revoke

    [Fact]
    public async Task RevokeMyToken_ValidRefreshToken_Returns200_AndRevokes()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();
        // Get a refresh token via login (CreateAuthenticatedClient discards it; re-login here)
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var refreshToken = loginBody!.Data!.RefreshToken;

        var revokeResponse = await client.PostAsJsonAsync("/api/v1/auth/revoke", new
        {
            refreshToken,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        // Revoked token can no longer be used to refresh
        var refreshResponse = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = loginBody.Data.Token, refreshToken,
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RevokeMyToken_AlreadyRevoked_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var refreshToken = loginBody!.Data!.RefreshToken;

        // First revoke succeeds
        await client.PostAsJsonAsync("/api/v1/auth/revoke", new { refreshToken }, ct);

        // Second revoke of the same token fails
        var second = await client.PostAsJsonAsync("/api/v1/auth/revoke", new { refreshToken }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Refresh token already revoked", body!.Error!.Message);
    }

    [Fact]
    public async Task RevokeMyToken_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/revoke", new
        {
            refreshToken = "anything",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RevokeMyToken_NonexistentToken_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/auth/revoke", new
        {
            refreshToken = "never-issued-token",
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Refresh token not found", body!.Error!.Message);
    }

    #endregion

    #region Forgot password / validate-reset-token / reset-password

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturns200_NoNotificationEnqueued()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = "nobody@nowhere.test",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, "nobody@nowhere.test");
        Assert.Empty(bodies);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_Returns200_AndEnqueuesResetEmail()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = "admin@localhost",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, "admin@localhost");
        Assert.NotEmpty(bodies);
        Assert.Contains("reset-password", bodies[0]);
    }

    [Fact]
    public async Task ValidateResetToken_ValidToken_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = "admin@localhost",
        }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "admin@localhost");

        var response = await Client.GetAsync(
            $"/api/v1/auth/validate-reset-token?email=admin@localhost&token={Uri.EscapeDataString(token)}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ChangesPasswordAndRevokesRefreshTokens()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a second user so we don't disrupt the per-test admin re-seed contract
        var userEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "resetuser@localhost", password: "OldPass123!");

        // Request reset
        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = userEmail }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, userEmail);

        var newPassword = "NewPass456!";
        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = userEmail,
            token,
            newPassword,
            confirmPassword = newPassword,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Old password no longer works
        var oldLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = userEmail, password = "OldPass123!",
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // New password works
        var newLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = userEmail, password = newPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenAlreadyUsed_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var userEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "reuse@localhost", password: "OldPass123!");

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = userEmail }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, userEmail);

        var newPassword = "NewPass456!";
        var first = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = userEmail, token, newPassword, confirmPassword = newPassword,
        }, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second use of the same token fails
        var second = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = userEmail, token, newPassword = "Another789!", confirmPassword = "Another789!",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_GarbageToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = "admin@localhost",
            token = "garbage",
            newPassword = "NewPass456!",
            confirmPassword = "NewPass456!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var userEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "weak@localhost", "OldPass123!");

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = userEmail }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, userEmail);

        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = userEmail,
            token,
            newPassword = "alllowercase", // no upper, no digit, no special
            confirmPassword = "alllowercase",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_PasswordsDoNotMatch_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var userEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "mismatch@localhost", password: "OldPass123!");

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = userEmail }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, userEmail);

        var response = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = userEmail,
            token,
            newPassword = "NewPass456!",
            confirmPassword = "Different789!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}