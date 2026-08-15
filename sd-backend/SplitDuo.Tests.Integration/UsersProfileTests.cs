using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Aliases.Dto;
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
        Assert.Equal("admin@splitduo.local", body.Data!.Email);
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
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
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

    #region GET /users/me/stats — alias-mode groups

    /// <summary>
    /// Creates an alias-mode group with two members, assigns both to a shared alias,
    /// finalizes alias setup, and returns the admin client, group id, admin user id,
    /// the shared alias id (as Guid string), and user2's singleton alias id.
    /// </summary>
    private async Task<(HttpClient adminClient, string groupId, string adminId, string aliasId, string user2SingletonAliasId)>
        SetupFinalizedAliasGroupAsync(string user2Email = "stats-alias@localhost")
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: true);
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            user2Email, "changeme123", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var user2Client = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await user2Client.GetCurrentUserAsync();

        // Capture user2's auto-created singleton alias before reassigning to the shared alias
        var aliasesBefore = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        var aliasesBeforeBody = await aliasesBefore.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var user2SingletonAliasId = aliasesBeforeBody!.Data!
            .Single(a => a.IsSingleton && a.Members.Any(m => m.Id == user2.Id)).Id;

        // Create a shared alias and assign both members to it
        var aliasResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases", new { name = "Couple" }, ct);
        aliasResponse.EnsureSuccessStatusCode();
        var aliasBody = await aliasResponse.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        var aliasId = aliasBody!.Data!.Id;

        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = admin.Id }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2.Id }, ct);

        // Finalize so expenses can be created
        var finalize = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases/finalize", new { }, ct);
        finalize.EnsureSuccessStatusCode();

        return (adminClient, group.Id, admin.Id, aliasId, user2SingletonAliasId);
    }

    [Fact]
    public async Task GetUserStats_AliasMode_ReturnsCorrectBalances()
    {
        // BUG: alias-mode balances ignored — see issue #16.
        // GetCurrentUserStatsAsync only computes individual-mode balances (paid_by +
        // expense_splits). Alias-mode groups never create ExpenseSplit rows, so the
        // alias-paid amount is counted but the alias-owed amount is not, inflating
        // YoureOwed. This test asserts the CORRECT alias-level behavior and is
        // EXPECTED TO FAIL until the bug is fixed.
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, aliasId, user2SingletonAliasId) = await SetupFinalizedAliasGroupAsync();

        // Admin's alias pays 100, split 50/50 between the two aliases (Couple + user2's singleton)
        await adminClient.CreateAliasExpenseAsync(groupId, adminId, amount: 100m,
            aliasSplits: new[]
            {
                new { aliasId, splitAmount = 50m },
                new { aliasId = user2SingletonAliasId, splitAmount = 50m },
            });

        var response = await adminClient.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserStatsDto>>(ct);
        Assert.Equal(1, body!.Data!.TotalGroups);
        // Alias paid 100, alias owed 50 → net +50 → user is owed 50
        Assert.Equal(50m, body.Data.YoureOwed);
        Assert.Equal(0m, body.Data.YouOwe);
    }

    [Fact]
    public async Task GetUserStats_IndividualMode_UnchangedByAliasFix()
    {
        // Regression guard: individual-mode groups must produce identical results
        // before and after the alias-mode fix.
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "stats-ind@localhost");
        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();

        // Admin pays 100 split 50/50 → admin +50, member -50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = member.Id, splitAmount = 50m },
            });

        // Member pays 60 split 30/30 → admin -30, member +30
        await memberClient.CreateExpenseAsync(group.Id, member.Id, amount: 60m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 30m },
                new { userId = member.Id, splitAmount = 30m },
            });

        var response = await client.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserStatsDto>>(ct);
        Assert.Equal(1, body!.Data!.TotalGroups);
        // Admin: paid 100, owed 80 → net +20
        Assert.Equal(20m, body.Data.YoureOwed);
        Assert.Equal(0m, body.Data.YouOwe);
    }

    [Fact]
    public async Task GetUserStats_MixedMode_CombinesBothCorrectly()
    {
        // BUG: alias-mode balances ignored — see issue #16.
        // The alias-mode group contributes paid but 0 owed, so the combined stats are
        // wrong. This test asserts the CORRECT combined behavior and is EXPECTED TO
        // FAIL until the bug is fixed.
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var admin = await client.GetCurrentUserAsync();

        // Individual-mode group: admin pays 100 split 50/50 → admin +50
        var individualGroup = await client.CreateGroupAsync();
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "stats-mixed@localhost");
        await client.PostAsJsonAsync(
            $"/api/v1/groups/{individualGroup.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();

        await client.CreateExpenseAsync(individualGroup.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = member.Id, splitAmount = 50m },
            });

        // Alias-mode group: admin's alias pays 100, alias owed 50 → admin +50
        var (adminClient, aliasGroupId, adminId, aliasId, user2SingletonAliasId) = await SetupFinalizedAliasGroupAsync("stats-mixed2@localhost");
        await adminClient.CreateAliasExpenseAsync(aliasGroupId, adminId, amount: 100m,
            aliasSplits: new[]
            {
                new { aliasId, splitAmount = 50m },
                new { aliasId = user2SingletonAliasId, splitAmount = 50m },
            });

        var response = await client.GetAsync("/api/v1/users/me/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserStatsDto>>(ct);
        Assert.Equal(2, body!.Data!.TotalGroups);
        // Individual +50, alias +50 → total owed 100
        Assert.Equal(100m, body.Data.YoureOwed);
        Assert.Equal(0m, body.Data.YouOwe);
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
            email = "admin@splitduo.local", password = "changeme123",
        }, ct);
        var loginBody = await login.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>(ct);
        var oldRefresh = loginBody!.Data!.RefreshToken;
        var oldToken = loginBody.Data.Token;

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new
        {
            currentPassword = "changeme123",
            newPassword = "NewPass456!",
            confirmPassword = "NewPass456!",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Old password no longer works
        var oldLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@splitduo.local", password = "changeme123",
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // New password works
        var newLogin = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@splitduo.local", password = "NewPass456!",
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
            currentPassword = "changeme123",
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
            currentPassword = "changeme123",
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
            currentPassword = "changeme123",
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
            currentPassword = "changeme123",
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
        Assert.Contains(body.Data, u => u.Email == "admin@splitduo.local");
        Assert.Contains(body.Data, u => u.Email == "extra@localhost");
    }

    [Fact]
    public async Task GetUsers_AsNonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "baseuser@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");

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
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
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
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
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
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
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
            email = "deletee@localhost", password = "changeme123",
        }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsNonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "nonadmin@splitduo.local");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "target2@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
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

    #region PUT /users/{userId} (admin edit user)

    [Fact]
    public async Task UpdateUser_AdminEditsNameAndEmailWithUnchangedRole_Succeeds()
    {
        // Regression: the admin edit form always sends globalRole (required field).
        // Editing only name/email must not 400 just because the role is unchanged.
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "editname@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            firstName = "Edited",
            lastName = "Name",
            email = "editname@localhost",
            globalRole = (int)GlobalRole.BaseUser, // unchanged from seeded role
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Edited", body!.Data!.FirstName);
        Assert.Equal("Name", body.Data.LastName);
        Assert.Equal((int)GlobalRole.BaseUser, body.Data.GlobalRoleId);

        // Verify persistence
        var get = await adminClient.GetAsync($"/api/v1/users/{target.Id}", ct);
        var getBody = await get.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Edited", getBody!.Data!.FirstName);
        Assert.Equal("Name", getBody.Data.LastName);
    }

    [Fact]
    public async Task UpdateUser_AdminEditsNameAndPromotesRole_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "editandpromote@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            firstName = "Promoted",
            email = "editandpromote@localhost",
            globalRole = (int)GlobalRole.SystemAdmin,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Promoted", body!.Data!.FirstName);
        Assert.Equal((int)GlobalRole.SystemAdmin, body.Data.GlobalRoleId);
    }

    [Fact]
    public async Task UpdateUser_AdminEditsNameAndDemotesRole_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a second admin so demotion is allowed (last-admin guard)
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "editanddemote@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = (int)GlobalRole.SystemAdmin,
        }, ct);

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            firstName = "Demoted",
            email = "editanddemote@localhost",
            globalRole = (int)GlobalRole.BaseUser,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Demoted", body!.Data!.FirstName);
        Assert.Equal((int)GlobalRole.BaseUser, body.Data.GlobalRoleId);
    }

    [Fact]
    public async Task UpdateUser_AdminEditsEmailToTaken_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        await TestDbSeeder.SeedUserAsync(Factory.Services, "taken@localhost");
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "editemail@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            email = "taken@localhost",
            globalRole = (int)GlobalRole.BaseUser,
        }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("User with this email already exists", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateUser_NonAdminEditingOtherUser_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "noneditor@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var admin = await (await CreateAuthenticatedClientAsync()).GetCurrentUserAsync();

        var response = await memberClient.PutAsJsonAsync($"/api/v1/users/{admin.Id}", new
        {
            firstName = "Hacked",
            globalRole = (int)GlobalRole.BaseUser,
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_NonAdminEditingSelfName_Succeeds()
    {
        // Non-admins can edit their own name/email but not their role.
        // Sending their own current role (unchanged) must be a no-op, not a 403.
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "selfeditor@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await memberClient.PutAsJsonAsync($"/api/v1/users/{member.Id}", new
        {
            firstName = "SelfEdited",
            globalRole = (int)GlobalRole.BaseUser, // unchanged — no-op, not forbidden
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("SelfEdited", body!.Data!.FirstName);
    }

    [Fact]
    public async Task UpdateUser_NonAdminTryingToPromoteSelf_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "selfpromoter@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await memberClient.PutAsJsonAsync($"/api/v1/users/{member.Id}", new
        {
            globalRole = (int)GlobalRole.SystemAdmin, // actual role change — non-admin forbidden
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Only system administrators can modify user roles", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateUser_InvalidGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.PutAsJsonAsync("/api/v1/users/not-a-guid", new
        {
            firstName = "X",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateUser_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", new
        {
            firstName = "Ghost",
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal("User not found", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateUser_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}", new
        {
            firstName = "X",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ChangeUserRole

    [Fact]
    public async Task ChangeUserRole_PromoteUser_ReturnsAdminRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a new base user
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "promotee@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        // Admin promotes them to SystemAdmin
        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 2,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.SystemAdmin, body!.Data!.GlobalRoleId);

        // Verify persistence
        var get = await adminClient.GetAsync($"/api/v1/users/{target.Id}", ct);
        var getBody = await get.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.SystemAdmin, getBody!.Data!.GlobalRoleId);
    }

    [Fact]
    public async Task ChangeUserRole_DemoteAdmin_ReturnsBaseUserRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a new user and promote them to admin
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "demotee@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 2,
        }, ct);

        // Admin demotes them back to BaseUser
        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 1,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, body!.Data!.GlobalRoleId);

        // Verify persistence
        var get = await adminClient.GetAsync($"/api/v1/users/{target.Id}", ct);
        var getBody = await get.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, getBody!.Data!.GlobalRoleId);
    }

    [Fact]
    public async Task ChangeUserRole_SelfDemotion_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{admin.Id}", new
        {
            globalRole = 1,
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("You cannot change your own role", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeUserRole_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "baseuser@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await memberClient.PutAsJsonAsync($"/api/v1/users/{member.Id}", new
        {
            globalRole = 2,
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Only system administrators can modify user roles", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeUserRole_SameRole_IsNoOp_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a new base user (already BaseUser)
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "duplicaterole@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        // Admin resubmits the user's current role — this is a no-op, not an error.
        // The admin edit form always sends globalRole (required field), so an edit
        // that only changes name/email must not 400 just because the role is unchanged.
        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = (int)GlobalRole.BaseUser,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, body!.Data!.GlobalRoleId);

        // Verify role is unchanged in persistence
        var get = await adminClient.GetAsync($"/api/v1/users/{target.Id}", ct);
        var getBody = await get.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, getBody!.Data!.GlobalRoleId);
    }

    [Fact]
    public async Task ChangeUserRole_InvalidRoleValue_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a new base user
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "invalidrole@localhost");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        // Admin tries to set an out-of-range role value
        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 99,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid role value", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeUserRole_DemoteOneOfTwoAdmins_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed a new user and promote them to admin (now there are 2 admins)
        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "secondadmin@splitduo.local");
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme123");
        var target = await targetClient.GetCurrentUserAsync();

        await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 2,
        }, ct);

        // Admin demotes the other admin (should succeed since >1 admin exists)
        var response = await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}", new
        {
            globalRole = 1,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, body!.Data!.GlobalRoleId);

        // Verify persistence
        var get = await adminClient.GetAsync($"/api/v1/users/{target.Id}", ct);
        var getBody = await get.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        Assert.Equal((int)GlobalRole.BaseUser, getBody!.Data!.GlobalRoleId);
    }

    #endregion
}