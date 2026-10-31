using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class EmailChangeTests : IntegrationTest
{
    public EmailChangeTests(SplitDuoApiFactory factory) : base(factory) { }

    private async Task<(string Email, string Guid)> SeedTargetAsync(string email = "target@localhost")
    {
        await TestDbSeeder.SeedUserAsync(Factory.Services, email);
        var guid = await TestDbSeeder.GetUserGuidAsync(Factory.Services, email);
        return (email, guid.ToString());
    }

    private async Task<string> RequestChangeAsync(
        HttpClient admin, string targetId, string newEmail, string password = "changeme123")
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail, currentPassword = password }, ct);
        response.EnsureSuccessStatusCode();

        return await NotificationTestExtensions.ExtractTokenFromFirstNotificationAsync(
            Factory.Services, EmailNormalizer.Normalize(newEmail));
    }

    private async Task<User> GetUserAsync(string email, bool includeDeleted = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalizedEmail = EmailNormalizer.Normalize(email);
        var query = db.Users.AsNoTracking().Where(u => u.Email == normalizedEmail);
        if (!includeDeleted)
            query = query.Where(u => u.DeletedAt == null);
        return await query.SingleAsync();
    }

    #region POST /users/{id}/email-change

    [Fact]
    public async Task RequestEmailChange_Admin_EnqueuesVerificationToNewAddress_AndStoresPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = " New@LocalHost ", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var bodies = await NotificationTestExtensions.GetEnqueuedBodiesAsync(Factory.Services, "new@localhost");
        Assert.Single(bodies);
        Assert.Contains("confirm-email-change?token=", bodies[0]);

        var target = await admin.GetFromJsonAsync<ApiResponseDto<UserDto>>(
            $"/api/v1/users/{targetId}", ct);
        Assert.Equal("target@localhost", target!.Data!.Email);
        Assert.Equal("new@localhost", target.Data.PendingEmail);
    }

    [Fact]
    public async Task RequestEmailChange_WrongAdminPassword_Returns401_AndSendsNoEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = "new@localhost", currentPassword = "wrong-password" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(await NotificationTestExtensions.GetEnqueuedBodiesAsync(Factory.Services, "new@localhost"));
    }

    [Fact]
    public async Task RequestEmailChange_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (memberEmail, _) = await SeedTargetAsync("member@localhost");
        var (_, targetId) = await SeedTargetAsync();
        var member = await CreateAuthenticatedClientAsync(memberEmail);

        var response = await member.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = "new@localhost", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequestEmailChange_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, targetId) = await SeedTargetAsync();

        var response = await Client.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = "new@localhost", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestEmailChange_TakenAddress_Returns409_CaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();
        await TestDbSeeder.SeedUserAsync(Factory.Services, "taken@localhost");

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = "TAKEN@localhost", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RequestEmailChange_SameAddress_IsNoOp_AndSendsNoEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (targetEmail, targetId) = await SeedTargetAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{targetId}/email-change",
            new { newEmail = targetEmail, currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await NotificationTestExtensions.GetEnqueuedBodiesAsync(Factory.Services, targetEmail));
    }

    [Fact]
    public async Task RequestEmailChange_AdminChangesOwnEmail_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var adminUser = await admin.GetCurrentUserAsync();

        var response = await admin.PostAsJsonAsync($"/api/v1/users/{adminUser.Id}/email-change",
            new { newEmail = "admin-new@localhost", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var bodies = await NotificationTestExtensions.GetEnqueuedBodiesAsync(
            Factory.Services, "admin-new@localhost");
        Assert.Single(bodies);
    }

    [Fact]
    public async Task Login_WithCaseVariantAddress_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await SeedTargetAsync("case@localhost");

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "CASE@LOCALHOST", password = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestEmailChange_UnknownTarget_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/users/{Guid.CreateVersion7()}/email-change",
            new { newEmail = "new@localhost", currentPassword = "changeme123" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST /auth/confirm-email-change

    [Fact]
    public async Task ConfirmEmailChange_ValidToken_AppliesChange_NotifiesOldAddress_RotatesStamp_AndForcesRelogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var oldEmail = "old@localhost";
        var (_, targetId) = await SeedTargetAsync(oldEmail);
        var before = await GetUserAsync(oldEmail);

        var token = await RequestChangeAsync(admin, targetId, "new@localhost");

        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var oldBodies = await NotificationTestExtensions.GetEnqueuedBodiesAsync(Factory.Services, oldEmail);
        Assert.Contains(oldBodies, b => b.Contains("new@localhost"));

        var after = await GetUserAsync("new@localhost");
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Null(after.PendingEmail);
        Assert.Null(after.PendingEmailTokenHash);
        Assert.Null(after.PendingEmailExpiresAt);

        var oldLogin = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = oldEmail, password = "changeme123" }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "new@localhost", password = "changeme123" }, ct);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_RevokesRefreshTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var oldEmail = "refresh@localhost";
        var (_, targetId) = await SeedTargetAsync(oldEmail);

        var login = await Client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = oldEmail, password = "changeme123" }, ct);
        var auth = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);

        var token = await RequestChangeAsync(admin, targetId, "refresh-new@localhost");
        await Client.PostAsJsonAsync("/api/v1/auth/confirm-email-change", new { token }, ct);

        var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = auth!.Data!.Token,
            refreshToken = auth.Data.RefreshToken,
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_ReusedToken_ReturnsGenericError()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();
        var token = await RequestChangeAsync(admin, targetId, "once@localhost");

        var first = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);
        var second = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_ExpiredToken_ReturnsGenericError()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();
        var token = await RequestChangeAsync(admin, targetId, "late@localhost");

        AdvanceTime(TimeSpan.FromHours(2));

        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Equal("target@localhost", (await GetUserAsync("target@localhost")).Email);
    }

    [Fact]
    public async Task ConfirmEmailChange_SecondRequestInvalidatesFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();

        var firstToken = await RequestChangeAsync(admin, targetId, "first@localhost");
        var secondToken = await RequestChangeAsync(admin, targetId, "second@localhost");

        var first = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token = firstToken }, ct);
        var second = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token = secondToken }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("second@localhost", (await GetUserAsync("second@localhost")).Email);
    }

    [Fact]
    public async Task ConfirmEmailChange_AddressTakenAfterRequest_FailsGenerically_AndClearsPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();
        var token = await RequestChangeAsync(admin, targetId, "race@localhost");

        await TestDbSeeder.SeedUserAsync(Factory.Services, "race@localhost");

        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        var target = await GetUserAsync("target@localhost");
        Assert.Equal("target@localhost", target.Email);
        Assert.Null(target.PendingEmail);
    }

    [Fact]
    public async Task ConfirmEmailChange_SoftDeletedTarget_FailsGenerically()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (oldEmail, targetId) = await SeedTargetAsync();
        var token = await RequestChangeAsync(admin, targetId, "gone-new@localhost");

        var delete = await admin.DeleteAsync($"/api/v1/users/{targetId}", ct);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Equal(oldEmail, (await GetUserAsync(oldEmail, includeDeleted: true)).Email);
    }

    [Fact]
    public async Task ConfirmEmailChange_KillsOutstandingPasswordResetToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (oldEmail, targetId) = await SeedTargetAsync();

        await Client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = oldEmail }, ct);
        var resetToken = await NotificationTestExtensions.ExtractTokenFromFirstNotificationAsync(
            Factory.Services, oldEmail);

        var changeToken = await RequestChangeAsync(admin, targetId, "reset-new@localhost");
        await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token = changeToken }, ct);

        var reset = await Client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = oldEmail,
            token = resetToken,
            newPassword = "NewPassword1!",
            confirmPassword = "NewPassword1!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_AllowsReuseOfSoftDeletedAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await CreateAuthenticatedClientAsync();
        var (_, targetId) = await SeedTargetAsync();
        var (_, deletedId) = await SeedTargetAsync("reuse@localhost");

        var delete = await admin.DeleteAsync($"/api/v1/users/{deletedId}", ct);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var token = await RequestChangeAsync(admin, targetId, "reuse@localhost");
        var confirm = await Client.PostAsJsonAsync(
            "/api/v1/auth/confirm-email-change", new { token }, ct);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("reuse@localhost", (await GetUserAsync("reuse@localhost")).Email);
    }

    #endregion
}
