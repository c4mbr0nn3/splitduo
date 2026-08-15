using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class GroupsTests : IntegrationTest
{
    public GroupsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Happy paths

    [Fact]
    public async Task CreateGroup_Returns200_WithGroupData()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Trip",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("Trip", body.Data.Name);
        Assert.Equal(1, body.Data.MemberCount);
        Assert.False(body.Data.UseAliases);
        Assert.False(body.Data.AliasSetupFinalized);
        Assert.Equal(0m, body.Data.NetBalance);
        Assert.NotEmpty(body.Data.CreatedByUserId);
        Assert.True(Guid.TryParse(body.Data.Id, out _));
        Assert.True(body.Data.CreatedAt > 0);
        Assert.True(body.Data.CreatedAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
    }

    [Fact]
    public async Task CreateGroup_WithDescription_PersistsDescription()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Trip",
            description = "Summer",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Summer", body!.Data!.Description);
    }

    [Fact]
    public async Task CreateGroup_WithUseAliases_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Alias Group",
            useAliases = true,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.True(body!.Data!.UseAliases);
        Assert.Equal(1, body.Data.MemberCount);
    }

    [Fact]
    public async Task CreateGroup_AppearsInList()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var listResponse = await client.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        Assert.NotNull(listBody!.Data);
        var match = listBody.Data.Single(g => g.Id == group.Id);
        Assert.Equal(1, match.MemberCount);
        Assert.Equal(0m, match.NetBalance);
    }

    [Fact]
    public async Task GetGroupById_ReturnsGroup()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync(name: "Trip");

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Trip", body!.Data!.Name);
        Assert.Equal(group.Id, body.Data.Id);
        Assert.NotEmpty(body.Data.CreatedByUserId);
        Assert.Equal(1, body.Data.MemberCount);
    }

    [Fact]
    public async Task UpdateGroup_Name_PersistsAndReturnsUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync(name: "Original");

        var putResponse = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}", new
        {
            name = "New Name",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("New Name", putBody!.Data!.Name);

        // Confirm via GET
        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}", ct);
        getResponse.EnsureSuccessStatusCode();
        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("New Name", getBody!.Data!.Name);
    }

    [Fact]
    public async Task UpdateGroup_Description_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync(name: "Trip");

        var putResponse = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}", new
        {
            description = "New desc",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("New desc", putBody!.Data!.Description);
    }

    [Fact]
    public async Task UpdateGroup_EmptyBody_LeavesGroupUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync(name: "Original");

        var putResponse = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Original", putBody!.Data!.Name);
    }

    [Fact]
    public async Task DeleteGroup_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.DeleteAsync($"/api/v1/groups/{group.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteGroup_RemovesFromList()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        await client.DeleteAsync($"/api/v1/groups/{group.Id}", ct);

        var listResponse = await client.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        Assert.Empty(listBody!.Data!);
    }

    [Fact]
    public async Task DeleteGroup_GetById_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        await client.DeleteAsync($"/api/v1/groups/{group.Id}", ct);

        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Group not found", getBody!.Error!.Message);
    }

    #endregion

    #region Validation (400, ProblemDetails)

    [Fact]
    public async Task CreateGroup_MissingName_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/groups", new { }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_NameTooLong_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var longName = new string('x', 201);
        var response = await client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = longName,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateGroup_NameTooLong_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var longName = new string('x', 201);
        var response = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}", new
        {
            name = longName,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Invalid Guid format (400, ApiResponseDto)

    [Fact]
    public async Task GetGroup_InvalidGuidFormat_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateGroup_InvalidGuidFormat_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/groups/not-a-guid", new
        {
            name = "Test",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteGroup_InvalidGuidFormat_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("/api/v1/groups/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    #endregion

    #region Auth — 401 unauthenticated

    [Fact]
    public async Task ListGroups_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/api/v1/groups", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/groups", new
        {
            name = "Trip",
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Not found — valid but nonexistent Guid

    [Fact]
    public async Task GetGroup_NonexistentGuid_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateGroup_NonexistentGuid_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync($"/api/v1/groups/{Guid.NewGuid()}", new
        {
            name = "Test",
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteGroup_NonexistentGuid_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"/api/v1/groups/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    #endregion

    #region Authorization — 403 not a member

    [Fact]
    public async Task GetGroup_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        // Seed second user and create a group as user2
        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();

        // Admin tries to GET user2's group
        var response = await adminClient.GetAsync($"/api/v1/groups/{user2Group.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateGroup_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();

        var response = await adminClient.PutAsJsonAsync($"/api/v1/groups/{user2Group.Id}", new
        {
            name = "Hacked",
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteGroup_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme123");
        var user2Group = await user2Client.CreateGroupAsync();

        var response = await adminClient.DeleteAsync($"/api/v1/groups/{user2Group.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    #endregion
}
