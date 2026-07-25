using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class UsersProfileTests : IntegrationTest
{
    public UsersProfileTests(SplitDuoApiFactory factory) : base(factory) { }

    #region GET /users/me

    [Fact]
    public async Task GetCurrentUser_ReturnsSeededAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/me", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("admin@localhost", body.Data!.Email);
        Assert.Equal("Super", body.Data.FirstName);
        Assert.Equal("Admin", body.Data.LastName);
        Assert.Equal((int)GlobalRole.SystemAdmin, body.Data.GlobalRoleId);
        Assert.False(body.Data.TwoFactorEnabled);
        Assert.NotEmpty(body.Data.Id);
        Assert.True(body.Data.CreatedAt > 0);
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/users/me", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /users/me/stats

    [Fact]
    public async Task GetUserStats_NoGroups_ReturnsZeros()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserStatsDto>>(ct);
        Assert.Equal(0, body!.Data!.TotalGroups);
        Assert.Equal(0m, body.Data.YouOwe);
        Assert.Equal(0m, body.Data.YoureOwed);
    }

    [Fact]
    public async Task GetUserStats_WithExpense_ReturnsOwedAndOwe()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "stats@localhost");
        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        // Admin pays 100, split 50/50 → admin +50, member -50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = member.Id, splitAmount = 50m },
            });

        var response = await client.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserStatsDto>>(ct);
        Assert.Equal(1, body!.Data!.TotalGroups);
        Assert.Equal(50m, body.Data.YoureOwed);
        Assert.Equal(0m, body.Data.YouOwe);
    }

    [Fact]
    public async Task GetUserStats_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PUT /users/me

    [Fact]
    public async Task UpdateCurrentUser_FirstName_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            firstName = "NewFirst",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("NewFirst", body!.Data!.FirstName);

        // Confirm via GET
        var me = await client.GetAsync("/api/v1/users/me", ct);
        var meBody = await me.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("NewFirst", meBody!.Data!.FirstName);
    }

    [Fact]
    public async Task UpdateCurrentUser_LastName_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            lastName = "NewLast",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("NewLast", body!.Data!.LastName);
    }

    [Fact]
    public async Task UpdateCurrentUser_Email_Persists_AndEnforcesUniqueness()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // Seed a second user with a different email
        await TestDbSeeder.SeedUserAsync(Factory.Services, "taken@localhost");

        // Try to take that email
        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            email = "taken@localhost",
        }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("User with this email already exists", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateCurrentUser_EmailToNewValidEmail_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            email = "newemail@localhost",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("newemail@localhost", body!.Data!.Email);
    }

    [Fact]
    public async Task UpdateCurrentUser_EmptyBody_LeavesUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var before = await client.GetCurrentUserAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal(before.FirstName, body!.Data!.FirstName);
        Assert.Equal(before.LastName, body!.Data!.LastName);
        Assert.Equal(before.Email, body.Data.Email);
    }

    [Fact]
    public async Task UpdateCurrentUser_InvalidEmail_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            email = "not-an-email",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrentUser_FirstNameTooLong_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            firstName = new string('x', 101),
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrentUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PutAsJsonAsync("/api/v1/users/me", new { }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PUT /users/me/password

    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_ChangesPassword_AndRevokesTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        // Get a refresh token via login
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var oldRefresh = loginBody!.Data!.RefreshToken;
        var oldToken = loginBody.Data.Token;

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme",
            newPassword = "NewPass456!",
            confirmPassword = "NewPass456!",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Old password no longer works
        var oldLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "changeme",
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // New password works
        var newLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@localhost", password = "NewPass456!",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        // Old refresh token revoked (password change revokes all tokens)
        var refresh = await Client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            token = oldToken, refreshToken = oldRefresh,
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "wrong-password",
            newPassword = "NewPass456!",
            confirmPassword = "NewPass456!",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Current password is incorrect", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangePassword_PasswordsDoNotMatch_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme",
            newPassword = "NewPass456!",
            confirmPassword = "Different789!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme",
            newPassword = "alllowercase",
            confirmPassword = "alllowercase",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_NewPasswordTooShort_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme",
            newPassword = "Short1!",
            confirmPassword = "Short1!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme",
            newPassword = "NewPass456!",
            confirmPassword = "NewPass456!",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /users (admin-only list)

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsList()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        await TestDbSeeder.SeedUserAsync(Factory.Services, "extra@localhost");

        var response = await client.GetAsync("/api/v1/users", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<UserDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.True(body.Data!.Count >= 2);
        Assert.Contains(body.Data, u => u.Email == "admin@localhost");
        Assert.Contains(body.Data, u => u.Email == "extra@localhost");
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "baseuser@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");

        var response = await memberClient.GetAsync("/api/v1/users", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/users", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET /users/{userId}

    [Fact]
    public async Task GetUser_OwnId_ReturnsSelf()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var me = await client.GetCurrentUserAsync();

        var response = await client.GetAsync($"/api/v1/users/{me.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal(me.Email, body!.Data!.Email);
    }

    [Fact]
    public async Task GetUser_AsNonAdminAccessingOther_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "m@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var admin = await (await CreateAuthenticatedClientAsync()).GetCurrentUserAsync();

        var response = await memberClient.GetAsync($"/api/v1/users/{admin.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("You can only access your own user data", body!.Error!.Message);
    }

    [Fact]
    public async Task GetUser_AsAdminAccessingOther_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "target@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.GetAsync($"/api/v1/users/{member.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("target@localhost", body!.Data!.Email);
    }

    [Fact]
    public async Task GetUser_InvalidGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task GetUser_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/users/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("User not found", body!.Error!.Message);
    }

    [Fact]
    public async Task GetUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/users/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /users/{userId} (admin-only)

    [Fact]
    public async Task DeleteUser_AsAdmin_SoftDeletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "deletee@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await adminClient.DeleteAsync($"/api/v1/users/{target.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deleted user no longer appears in the users list
        var list = await adminClient.GetAsync("/api/v1/users", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<UserDto>>>(ct);
        Assert.DoesNotContain(listBody!.Data!, u => u.Email == "deletee@localhost");

        // Deleted user can no longer log in
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "deletee@localhost", password = "changeme",
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsNonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "nonadmin@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "target2@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await memberClient.DeleteAsync($"/api/v1/users/{target.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_InvalidGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.DeleteAsync("/api/v1/users/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteUser_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("User not found", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}