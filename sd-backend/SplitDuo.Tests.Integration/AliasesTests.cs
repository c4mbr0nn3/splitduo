using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class AliasesTests : IntegrationTest
{
    public AliasesTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    /// <summary>
    /// Creates an alias-mode group, seeds a second member, and returns the admin client,
    /// group id, admin user id, and second user id. The creator starts with one singleton alias.
    /// </summary>
    private async Task<(HttpClient adminClient, string groupId, string adminId, string user2Id, HttpClient user2Client)>
        SetupAliasGroupWithTwoMembersAsync(string user2Email = "u2@localhost")
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: true);
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            user2Email, "changeme", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var user2Client = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var user2 = await user2Client.GetCurrentUserAsync();

        return (adminClient, group.Id, admin.Id, user2.Id, user2Client);
    }

    private static async Task<string> CreateAliasAsync(HttpClient client, string groupId, string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases", new { name }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        return body!.Data!.Id;
    }

    #endregion

    #region ListAliases

    [Fact]
    public async Task ListAliases_AliasModeGroup_ReturnsCreatorSingleton()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: true);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Single(body.Data!);
        Assert.True(body.Data[0].IsSingleton);
        Assert.Single(body.Data[0].Members);
    }

    [Fact]
    public async Task ListAliases_NonAliasModeGroup_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: false);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.Empty(body!.Data!);
    }

    [Fact]
    public async Task ListAliases_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme");
        var adminGroup = await adminClient.CreateGroupAsync(useAliases: true);

        var response = await otherClient.GetAsync($"/api/v1/groups/{adminGroup.Id}/aliases", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task ListAliases_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/aliases", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task ListAliases_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/aliases", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListAliases_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/aliases", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region CreateAlias

    [Fact]
    public async Task CreateAlias_AdminInAliasMode_Returns200_AndAppearsInList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases", new { name = "Couple" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("Couple", body.Data!.Name);
        Assert.False(body.Data.IsSingleton);
        Assert.Empty(body.Data.Members);

        // Appears in list
        var list = await adminClient.GetAsync($"/api/v1/groups/{groupId}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.Contains(listBody!.Data!, a => a.Name == "Couple");
    }

    [Fact]
    public async Task CreateAlias_DuplicateName_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        await CreateAliasAsync(adminClient, groupId, "Unique");

        var second = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases", new { name = "Unique" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("An alias with this name already exists in the group", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateAlias_NonAliasModeGroup_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: false);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Alias mode is not enabled for this group", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateAlias_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var response = await user2Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Only group administrators can manage aliases", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateAlias_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider2@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme");
        var adminGroup = await adminClient.CreateGroupAsync(useAliases: true);

        var response = await otherClient.PostAsJsonAsync(
            $"/api/v1/groups/{adminGroup.Id}/aliases", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateAlias_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/groups/not-a-guid/aliases", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateAlias_MissingName_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlias_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/aliases", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region UpdateAlias

    [Fact]
    public async Task UpdateAlias_Rename_Persists_AndDemotesSingleton()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        // Find the admin's singleton alias (by matching member) and rename it.
        // After setup there are two singletons (one per member), so we can't use .Single(IsSingleton).
        var list = await adminClient.GetAsync($"/api/v1/groups/{groupId}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var adminSingleton = listBody!.Data!.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == adminId));

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/aliases/{adminSingleton.Id}", new { name = "Renamed" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Renamed", body!.Data!.Name);
        Assert.False(body.Data.IsSingleton); // rename promotes singleton to named
    }

    [Fact]
    public async Task UpdateAlias_DuplicateName_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        // Use distinctive names that won't collide with auto-generated singleton names
        // (which are based on first names: "Super", "Second").
        var aliasId = await CreateAliasAsync(adminClient, groupId, "Alpha");
        await CreateAliasAsync(adminClient, groupId, "Beta");

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/aliases/{aliasId}", new { name = "Beta" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("An alias with this name already exists in the group", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateAlias_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await user2Client.PutAsJsonAsync(
            $"/api/v1/aliases/{aliasId}", new { name = "Y" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Only group administrators can manage aliases", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateAlias_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/aliases/{Guid.NewGuid()}", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Alias not found", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateAlias_InvalidAliasGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/aliases/not-a-guid", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Invalid alias ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateAlias_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PutAsJsonAsync(
            $"/api/v1/aliases/{Guid.NewGuid()}", new { name = "X" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region AssignMember

    [Fact]
    public async Task AssignMember_AddsUserToAlias()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "Couple");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Contains(body!.Data!.Members, m => m.Id == user2Id);
    }

    [Fact]
    public async Task AssignMember_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, user2Id, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await user2Client.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignMember_NonexistentUser_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = Guid.NewGuid().ToString() }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("User not found", body!.Error!.Message);
    }

    [Fact]
    public async Task AssignMember_UserNotGroupMember_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");
        // Seed a user that is NOT a member of the group
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider3@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme");
        var outsider = await outsiderClient.GetCurrentUserAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = outsider.Id }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("User is not a member of this group", body!.Error!.Message);
    }

    [Fact]
    public async Task AssignMember_InvalidAliasGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/aliases/not-a-guid/members", new { userId = Guid.NewGuid().ToString() }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Invalid alias ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task AssignMember_InvalidUserGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();
        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = "not-a-guid" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task AssignMember_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/aliases/{Guid.NewGuid()}/members", new { userId = Guid.NewGuid().ToString() }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region RemoveMember

    [Fact]
    public async Task RemoveMember_FromMultiPersonAlias_ReassignsToSingleton()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "Couple");
        // Assign both users to the alias
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = adminId }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);

        // Remove user2 from the alias
        var response = await adminClient.DeleteAsync(
            $"/api/v1/aliases/{aliasId}/members/{user2Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // user2 should now have a new singleton alias
        var list = await adminClient.GetAsync($"/api/v1/groups/{groupId}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.Contains(listBody!.Data!, a => a.Members.Any(m => m.Id == user2Id) && a.IsSingleton);
    }

    [Fact]
    public async Task RemoveMember_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, user2Id, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await user2Client.DeleteAsync(
            $"/api/v1/aliases/{aliasId}/members/{user2Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_InvalidAliasGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync(
            $"/api/v1/aliases/not-a-guid/members/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid alias ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveMember_InvalidUserGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();
        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await adminClient.DeleteAsync(
            $"/api/v1/aliases/{aliasId}/members/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveMember_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync(
            $"/api/v1/aliases/{Guid.NewGuid()}/members/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DeleteAlias

    [Fact]
    public async Task DeleteAlias_ReassignsMembersToSingletons_AndSoftDeletes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "Couple");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = adminId }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);

        var response = await adminClient.DeleteAsync($"/api/v1/aliases/{aliasId}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The deleted alias no longer appears in the list
        var list = await adminClient.GetAsync($"/api/v1/groups/{groupId}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        Assert.DoesNotContain(listBody!.Data!, a => a.Id == aliasId);

        // Both former members now have singleton aliases
        Assert.Contains(listBody.Data, a => a.IsSingleton && a.Members.Any(m => m.Id == adminId));
        Assert.Contains(listBody.Data, a => a.IsSingleton && a.Members.Any(m => m.Id == user2Id));
    }

    [Fact]
    public async Task DeleteAlias_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "X");

        var response = await user2Client.DeleteAsync($"/api/v1/aliases/{aliasId}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAlias_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"/api/v1/aliases/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Alias not found", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteAlias_InvalidAliasGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("/api/v1/aliases/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid alias ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteAlias_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync($"/api/v1/aliases/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region FinalizeAliasSetup

    [Fact]
    public async Task FinalizeAliasSetup_WithMultiPersonAlias_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "Couple");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = adminId }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify finalized flag via GET group
        var groupResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}", ct);
        var groupBody = await groupResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.True(groupBody!.Data!.AliasSetupFinalized);
    }

    [Fact]
    public async Task FinalizeAliasSetup_WithoutMultiPersonAlias_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, _) = await SetupAliasGroupWithTwoMembersAsync();

        // Only singletons exist (one per member) — no multi-person alias
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("At least one multi-person alias is required before finalizing", body!.Error!.Message);
    }

    [Fact]
    public async Task FinalizeAliasSetup_AlreadyFinalized_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, _) = await SetupAliasGroupWithTwoMembersAsync();

        var aliasId = await CreateAliasAsync(adminClient, groupId, "Couple");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = adminId }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{aliasId}/members", new { userId = user2Id }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases/finalize", new { }, ct);

        var second = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Alias setup is already finalized", body!.Error!.Message);
    }

    [Fact]
    public async Task FinalizeAliasSetup_NonAliasModeGroup_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: false);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Alias mode is not enabled for this group", body!.Error!.Message);
    }

    [Fact]
    public async Task FinalizeAliasSetup_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _, user2Client) = await SetupAliasGroupWithTwoMembersAsync();

        var response = await user2Client.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Only group administrators can finalize alias setup", body!.Error!.Message);
    }

    [Fact]
    public async Task FinalizeAliasSetup_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/groups/not-a-guid/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task FinalizeAliasSetup_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/aliases/finalize", new { }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}