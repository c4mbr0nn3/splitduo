using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Invitations.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class InvitationsTests : IntegrationTest
{
    public InvitationsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region SendInvitation — existing user (member_added)

    [Fact]
    public async Task SendInvitation_ExistingUser_AddsAsMember_AndEnqueuesNotification()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var inviteeEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "invitee@localhost", firstName: "Invitee");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = inviteeEmail }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("member_added", body!.Data!.Type);
        Assert.NotNull(body.Data.Member);
        Assert.Equal(inviteeEmail, body.Data.Member!.User.Email);
        Assert.Equal("member", body.Data.Member.Role);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, inviteeEmail);
        Assert.NotEmpty(bodies);
    }

    [Fact]
    public async Task SendInvitation_AlreadyMember_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var inviteeEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "dupe@localhost");

        // First invite succeeds (member_added)
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = inviteeEmail }, ct);

        // Second invite of the same existing user → 409
        var second = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = inviteeEmail }, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("User is already a member of this group", body!.Error!.Message);
    }

    #endregion

    #region SendInvitation — new user (invitation_sent)

    [Fact]
    public async Task SendInvitation_NewUser_CreatesPendingInvitation_AndEnqueuesEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "newuser@localhost" }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("invitation_sent", body!.Data!.Type);
        Assert.NotNull(body.Data.Invitation);
        Assert.Equal("newuser@localhost", body.Data.Invitation!.Email);
        Assert.Equal(group.Name, body.Data.Invitation.GroupName);
        Assert.NotEmpty(body.Data.Invitation.Id);

        var bodies = await NotificationTestExtensions
            .GetEnqueuedBodiesAsync(Factory.Services, "newuser@localhost");
        Assert.NotEmpty(bodies);
    }

    [Fact]
    public async Task SendInvitation_DuplicatePending_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "dupe@localhost" }, ct);

        var second = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "dupe@localhost" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("An invitation for this email is already pending", body!.Error!.Message);
    }

    #endregion

    #region SendInvitation — authorization

    [Fact]
    public async Task SendInvitation_NonAdminMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        // Seed a second user and add them as a regular member
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "member@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");

        var response = await memberClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "someone@localhost" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("Only group administrators can invite members", body!.Error!.Message);
    }

    [Fact]
    public async Task SendInvitation_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherClientEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "other@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherClientEmail, "changeme123");
        var adminGroup = await adminClient.CreateGroupAsync();

        var response = await otherClient.PostAsJsonAsync(
            $"/api/v1/groups/{adminGroup.Id}/invitations", new { email = "x@localhost" }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task SendInvitation_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/invitations", new { email = "x@localhost" }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendInvitation_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/groups/not-a-guid/invitations", new { email = "x@localhost" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task SendInvitation_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/invitations", new { email = "x@localhost" }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SendInvitationResponseDto>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task SendInvitation_InvalidEmail_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "not-an-email" }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region GetGroupInvitations

    [Fact]
    public async Task GetGroupInvitations_ReturnsOnlyPendingForGroup()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "a@localhost" }, ct);
        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "b@localhost" }, ct);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(2, body.Data!.Count);
        var emails = body.Data.Select(i => i.Email).OrderBy(e => e).ToList();
        Assert.Equal(new[] { "a@localhost", "b@localhost" }, emails);
    }

    [Fact]
    public async Task GetGroupInvitations_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "viewer@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");

        var response = await memberClient.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        Assert.Equal("Only group administrators can invite members", body!.Error!.Message);
    }

    #endregion

    #region ValidateInvitationToken

    [Fact]
    public async Task ValidateInvitationToken_ValidToken_ReturnsEmailAndGroupName()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(name: "Trip");

        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "validate@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "validate@localhost");

        var response = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={Uri.EscapeDataString(token)}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ValidateInvitationResponseDto>>(ct);
        Assert.Equal("validate@localhost", body!.Data!.Email);
        Assert.Equal("Trip", body.Data.GroupName);
    }

    [Fact]
    public async Task ValidateInvitationToken_GarbageToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync(
            "/api/v1/invitations/validate?token=garbage", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ValidateInvitationResponseDto>>(ct);
        Assert.Contains("invalid or has expired", body!.Error!.Message);
    }

    [Fact]
    public async Task ValidateInvitationToken_RevokedToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "rev@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "rev@localhost");

        // Fetch the invitation id and revoke it
        var list = await client.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        var invitationId = listBody!.Data!.Single(i => i.Email == "rev@localhost").Id;
        await client.DeleteAsync($"/api/v1/groups/{group.Id}/invitations/{invitationId}", ct);

        var response = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={Uri.EscapeDataString(token)}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ValidateInvitationResponseDto>>(ct);
        Assert.Contains("no longer valid", body!.Error!.Message);
    }

    #endregion

    #region AcceptInvitation

    [Fact]
    public async Task AcceptInvitation_CreatesAccount_AndAddsToGroup_AndCanLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "accept@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "accept@localhost");

        var response = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token,
            firstName = "Accepted",
            lastName = "User",
            password = "StrongPass1!",
            confirmPassword = "StrongPass1!",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // New user can log in
        var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "accept@localhost", password = "StrongPass1!",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        // New user appears in group members list
        var membersResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/members", ct);
        var membersBody = await membersResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Contains(membersBody!.Data!, m => m.User.Email == "accept@localhost");
    }

    [Fact]
    public async Task AcceptInvitation_ResolvesAllPendingInvitationsForEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group1 = await adminClient.CreateGroupAsync(name: "G1");
        var group2 = await adminClient.CreateGroupAsync(name: "G2");

        // Invite the same email to two different groups
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group1.Id}/invitations", new { email = "multi@localhost" }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group2.Id}/invitations", new { email = "multi@localhost" }, ct);

        // Extract the first invitation's token (only one token is needed — accept resolves all)
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "multi@localhost");

        var response = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token,
            firstName = "Multi",
            lastName = "User",
            password = "StrongPass1!",
            confirmPassword = "StrongPass1!",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // User should be a member of BOTH groups
        var members1 = await adminClient.GetAsync($"/api/v1/groups/{group1.Id}/members", ct);
        var members1Body = await members1.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Contains(members1Body!.Data!, m => m.User.Email == "multi@localhost");

        var members2 = await adminClient.GetAsync($"/api/v1/groups/{group2.Id}/members", ct);
        var members2Body = await members2.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupMemberDto>>>(ct);
        Assert.Contains(members2Body!.Data!, m => m.User.Email == "multi@localhost");
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyAccepted_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "twice@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "twice@localhost");

        var first = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token, firstName = "Twice", lastName = "User",
            password = "StrongPass1!", confirmPassword = "StrongPass1!",
        }, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token, firstName = "Twice", lastName = "User",
            password = "StrongPass1!", confirmPassword = "StrongPass1!",
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Contains("already been accepted", body!.Error!.Message);
    }

    [Fact]
    public async Task AcceptInvitation_GarbageToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token = "garbage",
            firstName = "X", lastName = "Y",
            password = "StrongPass1!", confirmPassword = "StrongPass1!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_PasswordsDoNotMatch_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "nomatch@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "nomatch@localhost");

        var response = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token, firstName = "No", lastName = "Match",
            password = "StrongPass1!", confirmPassword = "DifferentPass2!",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_WeakPassword_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "weak@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "weak@localhost");

        var response = await Client.PostAsJsonAsync("/api/v1/invitations/accept", new
        {
            token, firstName = "Weak", lastName = "Pwd",
            password = "alllowercase", confirmPassword = "alllowercase",
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region ResendInvitation

    [Fact]
    public async Task ResendInvitation_RevokesOldToken_AndCreatesNewToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "resend@localhost" }, ct);
        var oldToken = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "resend@localhost");

        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        var oldInvitationId = listBody!.Data!.Single(i => i.Email == "resend@localhost").Id;

        var resend = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations/{oldInvitationId}/resend", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var resendBody = await resend.Content.ReadFromJsonAsync<ApiResponseDto<InvitationDto>>(ct);
        Assert.NotEqual(oldInvitationId, resendBody!.Data!.Id);

        // Old token is now revoked
        var validateOld = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={Uri.EscapeDataString(oldToken)}", ct);
        Assert.Equal(HttpStatusCode.BadRequest, validateOld.StatusCode);

        // New token is valid
        var newToken = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "resend@localhost");
        var validateNew = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={Uri.EscapeDataString(newToken)}", ct);
        Assert.Equal(HttpStatusCode.OK, validateNew.StatusCode);
    }

    [Fact]
    public async Task ResendInvitation_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "rs@localhost" }, ct);
        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        var invitationId = listBody!.Data!.Single(i => i.Email == "rs@localhost").Id;

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "member2@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");

        var response = await memberClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations/{invitationId}/resend", new { }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResendInvitation_NonexistentInvitation_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations/{Guid.NewGuid()}/resend", new { }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<InvitationDto>>(ct);
        Assert.Equal("Invitation not found", body!.Error!.Message);
    }

    #endregion

    #region RevokeInvitation

    [Fact]
    public async Task RevokeInvitation_AdminRevokes_AndTokenBecomesInvalid()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "revoke@localhost" }, ct);
        var token = await NotificationTestExtensions
            .ExtractTokenFromFirstNotificationAsync(Factory.Services, "revoke@localhost");

        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        var invitationId = listBody!.Data!.Single(i => i.Email == "revoke@localhost").Id;

        var revoke = await adminClient.DeleteAsync(
            $"/api/v1/groups/{group.Id}/invitations/{invitationId}", ct);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        // Invitation no longer in the list
        var listAfter = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/invitations", ct);
        var listAfterBody = await listAfter.Content.ReadFromJsonAsync<ApiResponseDto<List<InvitationDto>>>(ct);
        Assert.DoesNotContain(listAfterBody!.Data!, i => i.Email == "revoke@localhost");

        // Token no longer validates
        var validate = await Client.GetAsync(
            $"/api/v1/invitations/validate?token={Uri.EscapeDataString(token)}", ct);
        Assert.Equal(HttpStatusCode.BadRequest, validate.StatusCode);
    }

    [Fact]
    public async Task RevokeInvitation_Nonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/invitations/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invitation not found", body!.Error!.Message);
    }

    [Fact]
    public async Task RevokeInvitation_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync(
            $"/api/v1/groups/not-a-guid/invitations/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    #endregion

    #region Pending invitations list (admin endpoint)

    [Fact]
    public async Task GetPendingInvitations_ReturnsPendingGroupedByEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/invitations", new { email = "pending@localhost" }, ct);

        var response = await adminClient.GetAsync("/api/v1/users/pending", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<PendingUserDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Contains(body.Data!, p => p.Email == "pending@localhost");
    }

    [Fact]
    public async Task GetPendingInvitations_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            email: "nonadmin@localhost");
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");

        var response = await memberClient.GetAsync("/api/v1/users/pending", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion
}