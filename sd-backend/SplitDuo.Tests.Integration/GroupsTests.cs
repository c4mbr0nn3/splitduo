using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
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

    #region Issue #15 — dashboard list balances & created-by

    /// <summary>
    /// Creates an alias-mode group with two members, a multi-person alias containing both,
    /// and finalizes alias setup. Returns the admin client, group id, admin user id, and
    /// the ids of the "Couple" alias and user2's singleton alias.
    /// </summary>
    private async Task<(HttpClient adminClient, string groupId, string adminId, string coupleAliasId, string user2SingletonAliasId)>
        SetupFinalizedAliasGroupAsync(string user2Email = "u2@localhost")
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

        // Capture the singleton alias ids while they still have members (they become
        // empty once both members are reassigned to the multi-person alias below).
        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var user2SingletonId = listBody!.Data!.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == user2.Id)).Id;

        // Create a multi-person alias and assign both members
        var aliasResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases", new { name = "Couple" }, ct);
        aliasResponse.EnsureSuccessStatusCode();
        var aliasBody = await aliasResponse.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        var coupleAliasId = aliasBody!.Data!.Id;

        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{coupleAliasId}/members", new { userId = admin.Id }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{coupleAliasId}/members", new { userId = user2.Id }, ct);

        // Finalize alias setup (required before expenses can be created)
        var finalizeResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases/finalize", new { }, ct);
        finalizeResponse.EnsureSuccessStatusCode();

        return (adminClient, group.Id, admin.Id, coupleAliasId, user2SingletonId);
    }

    /// <summary>
    /// Resolves the int DB ids for a group and user from their API-facing Guids.
    /// </summary>
    private async Task<(int groupId, int userId)> ResolveIntIdsAsync(string groupGuid, string userGuid)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await db.Groups.SingleAsync(g => g.Guid == Guid.Parse(groupGuid));
        var user = await db.Users.SingleAsync(u => u.Guid == Guid.Parse(userGuid));
        return (group.Id, user.Id);
    }

    [Fact]
    public async Task GetUserGroups_ReturnsCreatedByUserId()
    {
        // BUG: CreatedByUserId always empty — see issue #15.
        // The list query (Q2) includes Group but not Group.CreatedByUser and runs
        // AsNoTracking, so CreatedByUser is null and CreatedByUserId is "".
        // EXPECTED TO FAIL until the bug is fixed.
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var currentUser = await client.GetCurrentUserAsync();

        var group = await client.CreateGroupAsync(name: "Trip");

        var listResponse = await client.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        var match = listBody!.Data!.Single(g => g.Id == group.Id);
        Assert.Equal(currentUser.Id, match.CreatedByUserId);
    }

    [Fact]
    public async Task GetUserGroups_AliasMode_ReturnsCorrectNetBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, coupleAliasId, user2SingletonAliasId) =
            await SetupFinalizedAliasGroupAsync();

        // Admin (in "Couple" alias) pays 100; Couple owes 60, user2's singleton owes 40.
        // PaidByAliasId is set to the payer's current alias (Couple) by the service.
        var expenseResponse = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/expenses", new
        {
            title = "Dinner",
            amount = 100m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            aliasSplits = new[]
            {
                new { aliasId = coupleAliasId, splitAmount = 60m },
                new { aliasId = user2SingletonAliasId, splitAmount = 40m },
            },
        }, ct);
        expenseResponse.EnsureSuccessStatusCode();

        var listResponse = await adminClient.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        var match = listBody!.Data!.Single(g => g.Id == groupId);

        // Couple alias: paid 100, owed 60 → net +40
        Assert.Equal(40m, match.NetBalance);
    }

    [Fact]
    public async Task GetUserGroups_AliasMode_FallbackForNullPaidByAliasId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, _, _) = await SetupFinalizedAliasGroupAsync();

        // Pre-migration data: expense with paid_by_alias_id = NULL. The balance must
        // fall back to the payer's current alias membership (Couple).
        var (groupIntId, adminIntId) = await ResolveIntIdsAsync(groupId, adminId);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Expenses.Add(new Expense
            {
                GroupId = groupIntId,
                Title = "Legacy Expense",
                Amount = 50m,
                PaidBy = adminIntId,
                PaidByAliasId = null,
                ExpenseDate = new DateOnly(2025, 1, 15),
                CategoryId = 1,
                PaymentModeId = 1,
            });
            await db.SaveChangesAsync(ct);
        }

        var listResponse = await adminClient.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        var match = listBody!.Data!.Single(g => g.Id == groupId);

        // Expense attributes to admin's current alias (Couple): paid 50, owed 0 → net +50
        Assert.Equal(50m, match.NetBalance);
    }

    [Fact]
    public async Task GetUserGroups_IndividualMode_ReturnsNonZeroNetBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // Admin pays 100, split 50/50 → admin net +50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var listResponse = await client.GetAsync("/api/v1/groups", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<GroupDto>>>(ct);
        var match = listBody!.Data!.Single(g => g.Id == group.Id);

        Assert.Equal(50m, match.NetBalance);
    }

    #endregion
}
