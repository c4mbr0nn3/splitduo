using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class BalancesTests : IntegrationTest
{
    public BalancesTests(SplitDuoApiFactory factory) : base(factory) { }

    #region GetBalances — individual mode

    [Fact]
    public async Task GetBalances_EmptyGroup_ReturnsZeroBalanceForAllMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Single(body.Data!);
        Assert.Equal(0m, body.Data[0].Balance);
        Assert.Equal(0m, body.Data[0].TotalPaid);
        Assert.Equal(0m, body.Data[0].TotalOwed);
    }

    [Fact]
    public async Task GetBalances_SingleExpense_PayerOwedFullAmount()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // Admin pays 100, split 50/50 between admin and user2
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);
        Assert.Equal(2, body!.Data!.Count);

        var adminBalance = body.Data.Single(b => b.UserId == admin.Id);
        Assert.Equal(100m, adminBalance.TotalPaid);
        Assert.Equal(50m, adminBalance.TotalOwed);
        Assert.Equal(50m, adminBalance.Balance); // owed 50

        var user2Balance = body.Data.Single(b => b.UserId == user2Id);
        Assert.Equal(0m, user2Balance.TotalPaid);
        Assert.Equal(50m, user2Balance.TotalOwed);
        Assert.Equal(-50m, user2Balance.Balance); // owes 50
    }

    [Fact]
    public async Task GetBalances_MultipleExpenses_NetsCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, user2Client) = await SeedSecondMemberAsync(client, group.Id);

        // Admin pays 100 split 50/50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // User2 pays 60 split 30/30
        await user2Client.CreateExpenseAsync(group.Id, user2Id, amount: 60m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 30m },
                new { userId = user2Id, splitAmount = 30m },
            });

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);

        var adminBalance = body!.Data!.Single(b => b.UserId == admin.Id);
        // Paid 100, owed (50 + 30) = 80 → net +20
        Assert.Equal(100m, adminBalance.TotalPaid);
        Assert.Equal(80m, adminBalance.TotalOwed);
        Assert.Equal(20m, adminBalance.Balance);

        var user2Balance = body.Data.Single(b => b.UserId == user2Id);
        // Paid 60, owed (50 + 30) = 80 → net -20
        Assert.Equal(60m, user2Balance.TotalPaid);
        Assert.Equal(80m, user2Balance.TotalOwed);
        Assert.Equal(-20m, user2Balance.Balance);
    }

    [Fact]
    public async Task GetBalances_DeletedExpense_ExcludedFromTotals()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        var expense = await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // Delete the expense
        await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);
        Assert.Equal(2, body!.Data!.Count);
        Assert.All(body.Data, b => Assert.Equal(0m, b.Balance));
    }

    #endregion

    #region GetBalances — auth / errors

    [Fact]
    public async Task GetBalances_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/balances", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBalances_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/balances", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalances_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/balances", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalances_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme123");
        var adminGroup = await adminClient.CreateGroupAsync();

        var response = await otherClient.GetAsync($"/api/v1/groups/{adminGroup.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region GetBalanceSummary — suggestions

    [Fact]
    public async Task GetBalanceSummary_GeneratesSettlementSuggestion()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // Admin pays 100, split 50/50 → admin +50, user2 -50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<BalanceSummaryDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(group.Id, body.Data!.GroupId);
        Assert.Single(body.Data.Suggestions);
        var suggestion = body.Data.Suggestions[0];
        Assert.Equal(user2Id, suggestion.FromUserId);
        Assert.Equal(admin.Id, suggestion.ToUserId);
        Assert.Equal(50m, suggestion.Amount);
    }

    [Fact]
    public async Task GetBalanceSummary_NoDebt_ReturnsEmptySuggestions()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<BalanceSummaryDto>>(ct);
        Assert.Empty(body!.Data!.Suggestions);
    }

    [Fact]
    public async Task GetBalanceSummary_ThreeWaySplit_GeneratesMultipleSuggestions()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id, email: "u2@localhost");
        var (_, user3Id, _) = await SeedSecondMemberAsync(client, group.Id, email: "u3@localhost");

        // Admin pays 90, split 30/30/30 → admin +60, user2 -30, user3 -30
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 90m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 30m },
                new { userId = user2Id, splitAmount = 30m },
                new { userId = user3Id, splitAmount = 30m },
            });

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<BalanceSummaryDto>>(ct);
        // Two suggestions: user2→admin 30, user3→admin 30 (total 60)
        Assert.Equal(2, body!.Data!.Suggestions.Count);
        Assert.All(body.Data.Suggestions, s => Assert.Equal(admin.Id, s.ToUserId));
        Assert.Equal(60m, body.Data.Suggestions.Sum(s => s.Amount));
    }

    #endregion

    #region GetBalanceSummary — auth / errors

    [Fact]
    public async Task GetBalanceSummary_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBalanceSummary_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/balances/summary", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBalanceSummary_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalanceSummary_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider2@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme123");
        var adminGroup = await adminClient.CreateGroupAsync();

        var response = await otherClient.GetAsync(
            $"/api/v1/groups/{adminGroup.Id}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region GetGroupStats

    [Fact]
    public async Task GetGroupStats_ReturnsTotalsAndBreakdowns()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 50m,
            categoryId: 2, expenseDate: "2025-01-15"); // Groceries
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 30m,
            categoryId: 3, expenseDate: "2025-02-10"); // Transportation

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(2, body.Data!.TotalExpenses);
        Assert.Equal(80m, body.Data.TotalAmount);
        Assert.Equal(2, body.Data.CategoryBreakdown.Count);
        Assert.Contains(body.Data.CategoryBreakdown, c => c.CategoryId == 2 && c.Amount == 50m && c.Count == 1);
        Assert.Contains(body.Data.CategoryBreakdown, c => c.CategoryId == 3 && c.Amount == 30m && c.Count == 1);
        Assert.Equal(2, body.Data.MonthlyBreakdown.Count);
        Assert.Contains(body.Data.MonthlyBreakdown, m => m.Year == 2025 && m.Month == 1 && m.Amount == 50m);
        Assert.Contains(body.Data.MonthlyBreakdown, m => m.Year == 2025 && m.Month == 2 && m.Amount == 30m);
    }

    [Fact]
    public async Task GetGroupStats_EmptyGroup_ReturnsZeros()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.Equal(0, body!.Data!.TotalExpenses);
        Assert.Equal(0m, body.Data.TotalAmount);
        Assert.Empty(body.Data.CategoryBreakdown);
        Assert.Empty(body.Data.MonthlyBreakdown);
    }

    [Fact]
    public async Task GetGroupStats_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/stats", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupStats_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/stats", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupStats_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/stats", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGroupStats_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var otherEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider3@localhost");
        var otherClient = await CreateAuthenticatedClientAsync(otherEmail, "changeme123");
        var adminGroup = await adminClient.CreateGroupAsync();

        var response = await otherClient.GetAsync($"/api/v1/groups/{adminGroup.Id}/stats", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Alias-mode balances

    [Fact]
    public async Task GetBalances_AliasModeGroup_ReturnsAliasBalancesShape()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: true);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Alias-mode returns AliasBalanceDto list (different shape from BalanceDto)
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasBalanceDto>>>(ct);
        Assert.NotNull(body!.Data);
        // Creator has one singleton alias
        Assert.Single(body.Data!);
        Assert.True(body.Data[0].IsSingleton);
        Assert.Equal(0m, body.Data[0].Balance);
    }

    [Fact]
    public async Task GetBalanceSummary_AliasModeGroup_ReturnsAliasSummaryShape()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: true);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AliasBalanceSummaryDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(group.Id, body.Data!.GroupId);
        Assert.Single(body.Data.Balances);
        Assert.Empty(body.Data.Suggestions);
    }

    [Fact]
    public async Task GetGroupStats_AliasModeGroup_ReturnsEmptyBalancesList()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        // Alias-mode groups block expenses until AliasSetupFinalized, so we test with
        // an empty alias-mode group — which is enough to assert the empty-balances contract.
        var group = await client.CreateGroupAsync(useAliases: true);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(0, body.Data!.TotalExpenses);
        Assert.Equal(0m, body.Data.TotalAmount);
        // Alias-mode groups return empty per-user balances (per BalancesService comment)
        Assert.Empty(body.Data.Balances);
    }

    #endregion
}