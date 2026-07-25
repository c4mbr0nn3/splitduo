using System.Net;
using System.Net.Http.Json;
using OtpNet;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class TwoFactorTests : IntegrationTest
{
    public TwoFactorTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    /// <summary>
    /// Generates a current 6-digit TOTP code from a base32 secret string.
    /// Mirrors the server's OtpNet usage (Totp with VerificationWindow(1,1)).
    /// </summary>
    private static string GenerateTotpCode(string base32Secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.ComputeTotp();
    }

    /// <summary>
    /// Initiates 2FA setup for the given user and returns the setup DTO (secret + backup codes).
    /// </summary>
    private static async Task<TwoFactorSetupDto> InitiateSetupAsync(HttpClient client)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/initiate", new { }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<TwoFactorSetupDto>>(ct);
        return body!.Data!;
    }

    #endregion

    #region InitiateSetup

    [Fact]
    public async Task InitiateSetup_ReturnsSecret_QrUri_And10BackupCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/initiate", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<TwoFactorSetupDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.NotEmpty(body.Data!.Secret);
        Assert.StartsWith("otpauth://totp/", body.Data.QrCodeUri);
        Assert.Contains("SplitDuo", body.Data.QrCodeUri);
        Assert.Equal(10, body.Data.BackupCodes.Count);
        Assert.All(body.Data.BackupCodes, c => Assert.Matches(@"^[0-9a-f]{4}-[0-9a-f]{6}$", c));
    }

    [Fact]
    public async Task InitiateSetup_AlreadyEnabled_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // Complete a full setup first
        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        // Second initiation should fail
        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/initiate", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<TwoFactorSetupDto>>(ct);
        Assert.Equal("Two-factor authentication is already enabled", body!.Error!.Message);
    }

    [Fact]
    public async Task InitiateSetup_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/2fa/setup/initiate", new { }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region VerifySetup

    [Fact]
    public async Task VerifySetup_ValidCode_Enables2FA_AndEnqueuesEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 2FA is now enabled — reflected on /users/me
        var me = await client.GetAsync("/api/v1/users/me", ct);
        var meBody = await me.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.True(meBody!.Data!.TwoFactorEnabled);

        // Confirmation email enqueued
        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, "admin@localhost");
        Assert.Contains(bodies, b => b.Contains("two-factor") || b.Contains("2FA") || b.Contains("enabled"));
    }

    [Fact]
    public async Task VerifySetup_InvalidCode_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/verify",
            new { code = "000000" }, ct);

        // "000000" is unlikely to be the current valid code; if it happens to match,
        // the test would pass the wrong branch, so we accept either 400 or 200 here
        // but assert the non-success path is the expected one in practice.
        // A robust approach: use a clearly-wrong code that still passes model validation.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
            Assert.Equal("Invalid verification code", body!.Error!.Message);
        }
        else
        {
            // If 000000 happened to be valid, force a different invalid code
            var altResponse = await client.PostAsJsonAsync("/api/v1/2fa/setup/verify",
                new { code = "999999" }, ct);
            Assert.Equal(HttpStatusCode.BadRequest, altResponse.StatusCode);
        }
    }

    [Fact]
    public async Task VerifySetup_NotInitiated_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/verify",
            new { code = "123456" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Two-factor setup not initiated", body!.Error!.Message);
    }

    [Fact]
    public async Task VerifySetup_CodeTooShort_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        await InitiateSetupAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/setup/verify",
            new { code = "123" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifySetup_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/2fa/setup/verify",
            new { code = "123456" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Disable

    [Fact]
    public async Task Disable_WithValidPassword_Disables2FA_AndEnqueuesEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/disable",
            new { password = "changeme" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await client.GetAsync("/api/v1/users/me", ct);
        var meBody = await me.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.False(meBody!.Data!.TwoFactorEnabled);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, "admin@localhost");
        Assert.Contains(bodies, b => b.Contains("disabled"));
    }

    [Fact]
    public async Task Disable_WithWrongPassword_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/disable",
            new { password = "wrong-password" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid password", body!.Error!.Message);
    }

    [Fact]
    public async Task Disable_WhenNotEnabled_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/2fa/disable",
            new { password = "changeme" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Two-factor authentication is not enabled", body!.Error!.Message);
    }

    [Fact]
    public async Task Disable_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/2fa/disable",
            new { password = "changeme" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GenerateBackupCodes

    [Fact]
    public async Task GenerateBackupCodes_When2FAEnabled_Returns10NewCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var response = await client.PostAsJsonAsync("/api/v1/2fa/backup-codes/generate", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<string>>>(ct);
        Assert.Equal(10, body!.Data!.Count);
        Assert.All(body.Data, c => Assert.Matches(@"^[0-9a-f]{4}-[0-9a-f]{6}$", c));
        // New codes differ from the originals
        Assert.NotEqual(setup.BackupCodes.OrderBy(c => c), body.Data.OrderBy(c => c));
    }

    [Fact]
    public async Task GenerateBackupCodes_When2FADisabled_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/2fa/backup-codes/generate", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<string>>>(ct);
        Assert.Equal("Two-factor authentication is not enabled", body!.Error!.Message);
    }

    [Fact]
    public async Task GenerateBackupCodes_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/2fa/backup-codes/generate", new { }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Login flow with 2FA enabled

    [Fact]
    public async Task Login_With2FAEnabled_ReturnsRequiresTwoFactor_AndNoAccessToken()
    {
        var var = TestContext.Current.CancellationToken;
        var ct = var;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        // Login now requires 2FA
        var loginResponse = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.True(loginBody!.Data!.RequiresTwoFactor);
        Assert.NotEmpty(loginBody.Data.TwoFactorChallengeToken ?? "");
        Assert.Empty(loginBody.Data.Token); // no access token until 2FA verified
    }

    [Fact]
    public async Task Login_With2FA_VerifyWithTotpCode_ReturnsAccessToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var challengeToken = loginBody!.Data!.TwoFactorChallengeToken!;

        var totpCode = GenerateTotpCode(setup.Secret);
        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
        {
            challengeToken, code = totpCode, codeType = "totp",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotEmpty(verifyBody!.Data!.Token);
        Assert.NotEmpty(verifyBody.Data.RefreshToken);
        Assert.False(verifyBody.Data.RequiresTwoFactor);
    }

    [Fact]
    public async Task Login_With2FA_VerifyWithBackupCode_ReturnsAccessToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var challengeToken = loginBody!.Data!.TwoFactorChallengeToken!;

        // Use one of the backup codes returned during setup
        var backupCode = setup.BackupCodes[0];
        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
        {
            challengeToken, code = backupCode, codeType = "backup",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.NotEmpty(verifyBody!.Data!.Token);
    }

    [Fact]
    public async Task Login_With2FA_VerifyWithInvalidTotp_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var challengeToken = loginBody!.Data!.TwoFactorChallengeToken!;

        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
        {
            challengeToken, code = "000000", codeType = "totp",
        }, ct);

        // 000000 is unlikely valid; accept 401 (invalid code) or 400 (if it happened to match)
        if (verifyResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            var body = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
            Assert.Equal("Invalid verification code", body!.Error!.Message);
        }
        else
        {
            // Retry with a different invalid code
            var retry = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
            {
                challengeToken, code = "999999", codeType = "totp",
            }, ct);
            Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
        }
    }

    [Fact]
    public async Task Login_With2FA_VerifyWithInvalidChallengeToken_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
        {
            challengeToken = "garbage.token.here", code = "123456", codeType = "totp",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, verifyResponse.StatusCode);
        var body = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Invalid or expired challenge token", body!.Error!.Message);
    }

    [Fact]
    public async Task Login_With2FA_VerifyWithInvalidCodeType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var setup = await InitiateSetupAsync(client);
        var code = GenerateTotpCode(setup.Secret);
        await client.PostAsJsonAsync("/api/v1/2fa/setup/verify", new { code }, ct);

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var challengeToken = loginBody!.Data!.TwoFactorChallengeToken!;

        var verifyResponse = await Client.PostAsJsonAsync("/api/v1/auth/verify-2fa", new
        {
            challengeToken, code = "123456", codeType = "invalid",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
        var body = await verifyResponse.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        Assert.Equal("Invalid code type", body!.Error!.Message);
    }

    #endregion
}