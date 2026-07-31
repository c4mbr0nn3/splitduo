using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class GroupMembersTests : IntegrationTest
{
    public GroupMembersTests(SplitDuoApiFactory factory) : base(factory) { }

    #region GetGroupMembers — happy path

    [Fact]
    public async Task GetGroupMembers_ReturnsCreatorAsAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/members", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Single(body.Data!);
        Assert.Equal("admin", body.Data[0].Role);
        Assert.Equal("admin@localhost", body.Data[0].User.Email);
        Assert.Equal(group.Id, body.Data[0].GroupId);
        Assert.NotEmpty(body.Data[0].UserId);
        Assert.True(body.Data[0].JoinedAt > 0);
    }

    [Fact]
    public async Task GetGroupMembers_AfterAdd_ReturnsBothOrderedByJoinedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "member@localhost", "changeme", "Added", "Member");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        var response = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/members", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Equal(2, body!.Data!.Count);
        // Ordered by CreatedAt ascending — admin (creator) first
        Assert.Equal("admin@localhost", body.Data[0].User.Email);
        Assert.Equal("admin", body.Data[0].Role);
        Assert.Equal("member@localhost", body.Data[1].User.Email);
        Assert.Equal("member", body.Data[1].Role);
    }

    #endregion

    #region GetGroupMembers — auth / errors

    [Fact]
    public async Task GetGroupMembers_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/members", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupMembers_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/members", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task GetGroupMembers_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/members", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task GetGroupMembers_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "other@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme");
        var adminGroup = await adminClient.CreateGroupAsync();

        var response = await otherClient.GetAsync($"/api/v1/groups/{adminGroup.Id}/members", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    #endregion

    #region AddGroupMember — happy path

    [Fact]
    public async Task AddGroupMember_ExistingUser_AddsAsMember_AndReturnsDto()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "newmember@localhost", "changeme", "New", "Member");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("newmember@localhost", body.Data!.User.Email);
        Assert.Equal("member", body.Data.Role);
        Assert.Equal(group.Id, body.Data.GroupId);
        Assert.NotEmpty(body.Data.UserId);
        Assert.True(body.Data.JoinedAt > 0);
    }

    [Fact]
    public async Task AddGroupMember_DefaultRoleIsMember()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "def@localhost");

        // Omit role — should default to "member"
        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("member", body!.Data!.Role);
    }

    [Fact]
    public async Task AddGroupMember_AdminRole_Persists()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "admin2@localhost");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("admin", body!.Data!.Role);
    }

    [Fact]
    public async Task AddGroupMember_AliasModeGroup_AutoCreatesSingletonAlias()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: true);

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "alias@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var newMember = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The new member should have a singleton alias assigned — verify via the aliases endpoint
        var aliasesResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        Assert.Equal(HttpStatusCode.OK, aliasesResponse.StatusCode);
        var aliasesBody = await aliasesResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        // Creator + new member = 2 singleton aliases
        Assert.Equal(2, aliasesBody!.Data!.Count);
        // The new member's user id should appear in one of the aliases' member lists
        Assert.Contains(aliasesBody.Data, a => a.Members.Any(m => m.Id == newMember.Id));
    }

    #endregion

    #region AddGroupMember — errors

    [Fact]
    public async Task AddGroupMember_AlreadyMember_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "dupe@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        var second = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("User is already a member of this group", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_UserNotFound_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members",
            new { userEmail = "nobody@nowhere.test", role = "member" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("User with this email not found", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_NonAdminMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "peasant@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");

        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "target@localhost");
        var response = await memberClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = targetEmail, role = "member" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("Only group administrators can add members", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme");
        var adminGroup = await adminClient.CreateGroupAsync();

        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "target2@localhost");
        var response = await otherClient.PostAsJsonAsync(
            $"/api/v1/groups/{adminGroup.Id}/members", new { userEmail = targetEmail, role = "member" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/members",
            new { userEmail = "x@localhost", role = "member" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddGroupMember_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/groups/not-a-guid/members",
            new { userEmail = "x@localhost", role = "member" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/members",
            new { userEmail = "admin@localhost", role = "member" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task AddGroupMember_InvalidEmail_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = "not-an-email", role = "member" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region RemoveGroupMember — happy path

    [Fact]
    public async Task RemoveGroupMember_AdminRemovesMember_Succeeds_AndMemberLosesAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "gone@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Removed member can no longer access the group
        var forbidden = await memberClient.GetAsync($"/api/v1/groups/{group.Id}", ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Member count drops back to 1
        var members = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/members", ct);
        var membersBody = await members.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Single(membersBody!.Data!);
    }

    [Fact]
    public async Task RemoveGroupMember_SelfRemoval_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "self@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await memberClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region RemoveGroupMember — errors

    [Fact]
    public async Task RemoveGroupMember_LastAdmin_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var response = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{admin.Id}", ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Cannot remove the only administrator of the group", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_NonAdminRemovingOther_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var m1Email = await TestDbSeeder.SeedUserAsync(Factory.Services, "m1@localhost");
        var m2Email = await TestDbSeeder.SeedUserAsync(Factory.Services, "m2@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = m1Email, role = "member" }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = m2Email, role = "member" }, ct);

        var m1Client = await CreateAuthenticatedClientAsync(m1Email, "changeme");
        var m2 = await (await CreateAuthenticatedClientAsync(m2Email, "changeme")).GetCurrentUserAsync();

        var response = await m1Client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{m2.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("You can only remove yourself or, as an admin, remove other members", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_NonexistentMember_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("User to remove not found", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_NonMemberGroup_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider2@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme");
        var adminGroup = await adminClient.CreateGroupAsync();

        // Use a real user (the admin) as the removal target — the service checks
        // user existence before membership, so a nonexistent user would yield 404
        // ("User to remove not found") rather than the 403 we want to assert here.
        var admin = await adminClient.GetCurrentUserAsync();

        var response = await otherClient.DeleteAsync(
            $"/api/v1/groups/{adminGroup.Id}/members/{admin.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync(
            $"/api/v1/groups/not-a-guid/members/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_InvalidUserGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task RemoveGroupMember_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.DeleteAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/members/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ChangeMemberRole

    [Fact]
    public async Task ChangeMemberRole_PromoteMember_ReturnsAdminRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "promotee@localhost", "changeme", "Promotee", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}/role", new { role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("admin", body.Data!.Role);
        Assert.Equal(member.Id, body.Data.UserId);
        Assert.Equal(memberEmail, body.Data.User.Email);

        // Verify persistence
        var membersResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/members", ct);
        var membersBody = await membersResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        var target = membersBody!.Data!.FirstOrDefault(m => m.UserId == member.Id);
        Assert.NotNull(target);
        Assert.Equal("admin", target!.Role);
    }

    [Fact]
    public async Task ChangeMemberRole_DemoteAdmin_ReturnsMemberRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var secondEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "demotee@localhost", "changeme", "Demotee", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = secondEmail, role = "admin" }, ct);
        var secondClient = await CreateAuthenticatedClientAsync(secondEmail, "changeme");
        var second = await secondClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{second.Id}/role", new { role = "member" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("member", body.Data!.Role);
        Assert.Equal(second.Id, body.Data.UserId);

        // Verify persistence
        var membersResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/members", ct);
        var membersBody = await membersResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        var target = membersBody!.Data!.FirstOrDefault(m => m.UserId == second.Id);
        Assert.NotNull(target);
        Assert.Equal("member", target!.Role);
    }

    [Fact]
    public async Task ChangeMemberRole_SelfDemotion_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{admin.Id}/role", new { role = "member" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("You cannot change your own role", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_DemoteOneOfTwoAdmins_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var secondEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "secondadmin@localhost", "changeme", "Second", "Admin");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = secondEmail, role = "admin" }, ct);
        var secondClient = await CreateAuthenticatedClientAsync(secondEmail, "changeme");
        var second = await secondClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{second.Id}/role", new { role = "member" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupMemberDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal("member", body.Data!.Role);
    }

    [Fact]
    public async Task ChangeMemberRole_NonAdminMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "regular@localhost", "changeme", "Regular", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");

        var targetEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "target@localhost", "changeme", "Target", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = targetEmail, role = "member" }, ct);
        var targetClient = await CreateAuthenticatedClientAsync(targetEmail, "changeme");
        var target = await targetClient.GetCurrentUserAsync();

        var response = await memberClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{target.Id}/role", new { role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Only group administrators can change member roles", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_InvalidRole_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "invalidrole@localhost", "changeme", "Invalid", "Role");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}/role", new { role = "superadmin" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid role. Must be 'admin' or 'member'.", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_DuplicateRole_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "duperole@localhost", "changeme", "Dupe", "Role");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}/role", new { role = "member" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("User already has this role", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_NonMemberCaller_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "member@localhost", "changeme", "Member", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var member = await memberClient.GetCurrentUserAsync();

        // Seed a third user who is NOT a member of the group
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme");

        var response = await outsiderClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{member.Id}/role", new { role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_TargetNotInGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "member2@localhost", "changeme", "Member", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);

        // Seed a third user who is NOT a member of the group
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider2@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme");
        var outsider = await outsiderClient.GetCurrentUserAsync();

        // Admin tries to change role of a user who is not in the group
        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{outsider.Id}/role", new { role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("User is not a member of this group", body!.Error!.Message);
    }

    [Fact]
    public async Task ChangeMemberRole_NonexistentTargetUser_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members/{Guid.NewGuid()}/role", new { role = "admin" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("User not found", body!.Error!.Message);
    }

    #endregion
}