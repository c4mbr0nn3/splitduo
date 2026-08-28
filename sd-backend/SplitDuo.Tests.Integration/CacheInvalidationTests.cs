using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class CacheHitMissTests : IntegrationTest
{
    public CacheHitMissTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Balances — cache hit/miss

    [Fact]
    public async Task GetBalances_RepeatedCall_ReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var response1 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);

        var response2 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(body1!.Data!.Count, body2!.Data!.Count);
        Assert.Equal(body1.Data[0].Balance, body2.Data[0].Balance);
        Assert.Equal(body1.Data[0].TotalPaid, body2.Data[0].TotalPaid);
    }

    #endregion

    #region Balance Summary — cache hit/miss

    [Fact]
    public async Task GetBalanceSummary_RepeatedCall_ReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var response1 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<BalanceSummaryDto>>(ct);

        var response2 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<BalanceSummaryDto>>(ct);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(body1!.Data!.Suggestions.Count, body2!.Data!.Suggestions.Count);
        Assert.Equal(body1.Data.Suggestions[0].Amount, body2.Data.Suggestions[0].Amount);
    }

    #endregion

    #region Group Stats — cache hit/miss

    [Fact]
    public async Task GetGroupStats_RepeatedCall_ReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 50m, categoryId: 2, expenseDate: "2025-01-15");
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 30m, categoryId: 3, expenseDate: "2025-02-10");

        var response1 = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);

        var response2 = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(body1!.Data!.ExpenseCount, body2!.Data!.ExpenseCount);
        Assert.Equal(body1.Data.TotalAmount, body2.Data.TotalAmount);
        Assert.Equal(body1.Data.CategoryBreakdown.Count, body2.Data.CategoryBreakdown.Count);
    }

    #endregion

    #region Alias-mode balances — cache hit/miss

    [Fact]
    public async Task GetBalances_AliasMode_RepeatedCall_ReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: true);

        var response1 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasBalanceDto>>>(ct);

        var response2 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasBalanceDto>>>(ct);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(body1!.Data!.Count, body2!.Data!.Count);
        Assert.Equal(body1.Data[0].Balance, body2.Data[0].Balance);
    }

    [Fact]
    public async Task GetBalanceSummary_AliasMode_RepeatedCall_ReturnsSameResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync(useAliases: true);

        var response1 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponseDto<AliasBalanceSummaryDto>>(ct);

        var response2 = await client.GetAsync($"/api/v1/groups/{group.Id}/balances/summary", ct);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponseDto<AliasBalanceSummaryDto>>(ct);

        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        Assert.Equal(body1!.Data!.Balances.Count, body2!.Data!.Balances.Count);
        Assert.Equal(body1.Data.Suggestions.Count, body2.Data.Suggestions.Count);
    }

    #endregion
}

public class CacheInvalidationTests : IntegrationTest
{
    public CacheInvalidationTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private async Task<ApiResponseDto<List<BalanceDto>>> GetBalancesAsync(HttpClient client, string groupId)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.GetAsync($"/api/v1/groups/{groupId}/balances", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct))!;
    }

    #endregion

    #region Expense write invalidation

    [Fact]
    public async Task GetBalances_CreateExpense_InvalidatesCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // First expense: 100 split 50/50
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // Warm the cache — record the admin balance (50)
        var before = await GetBalancesAsync(client, group.Id);
        var adminBefore = before.Data!.Single(b => b.UserId == admin.Id);
        Assert.Equal(50m, adminBefore.Balance);

        // Second expense: 100 split 50/50 — must invalidate the cached balances
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 100m,
            splits: new[]
            {
                new { userId = admin.Id, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        var afterSecond = await GetBalancesAsync(client, group.Id);
        var adminAfterSecond = afterSecond.Data!.Single(b => b.UserId == admin.Id);
        Assert.Equal(100m, adminAfterSecond.Balance);
    }

    [Fact]
    public async Task GetGroupStats_CreateExpense_InvalidatesCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var admin = await client.GetCurrentUserAsync();

        // Warm the cache with an empty-group stats
        var warmResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);
        warmResponse.EnsureSuccessStatusCode();
        var warm = await warmResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.Equal(0, warm!.Data!.ExpenseCount);

        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 50m, categoryId: 2, expenseDate: "2025-01-15");

        var afterFirstResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);
        afterFirstResponse.EnsureSuccessStatusCode();
        var afterFirst = await afterFirstResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.Equal(1, afterFirst!.Data!.ExpenseCount);
        Assert.Equal(50m, afterFirst.Data.TotalAmount);

        // Second expense — must invalidate the cached stats
        await client.CreateExpenseAsync(group.Id, admin.Id, amount: 30m, categoryId: 3, expenseDate: "2025-02-10");

        var afterSecondResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/stats", ct);
        afterSecondResponse.EnsureSuccessStatusCode();
        var afterSecond = await afterSecondResponse.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.Equal(2, afterSecond!.Data!.ExpenseCount);
        Assert.Equal(80m, afterSecond.Data.TotalAmount);
    }

    [Fact]
    public async Task GetBalances_DeleteExpense_InvalidatesCache()
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

        // Warm the cache with non-zero balances
        var warm = await GetBalancesAsync(client, group.Id);
        Assert.Equal(2, warm.Data!.Count);
        Assert.Contains(warm.Data, b => b.Balance != 0m);

        // Delete the expense — must invalidate the cached balances
        var deleteResponse = await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var afterDelete = await GetBalancesAsync(client, group.Id);
        Assert.Equal(2, afterDelete.Data!.Count);
        Assert.All(afterDelete.Data, b => Assert.Equal(0m, b.Balance));
    }

    [Fact]
    public async Task GetBalances_UpdateExpense_InvalidatesCache()
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

        // Warm the cache with the 100/50-50 balances
        var warm = await GetBalancesAsync(client, group.Id);
        var adminWarm = warm.Data!.Single(b => b.UserId == admin.Id);
        Assert.Equal(50m, adminWarm.Balance);

        // Update the expense: amount 200, split 100/100 — must invalidate the cached balances
        var putResponse = await client.PutAsJsonAsync(
            $"/api/v1/groups/{group.Id}/expenses/{expense.Id}", new
            {
                amount = 200m,
                splits = new[]
                {
                    new { userId = admin.Id, splitAmount = 100m },
                    new { userId = user2Id, splitAmount = 100m },
                },
            }, ct);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var afterUpdate = await GetBalancesAsync(client, group.Id);
        var adminAfterUpdate = afterUpdate.Data!.Single(b => b.UserId == admin.Id);
        Assert.Equal(200m, adminAfterUpdate.TotalPaid);
        Assert.Equal(100m, adminAfterUpdate.TotalOwed);
        Assert.Equal(100m, adminAfterUpdate.Balance);

        var user2AfterUpdate = afterUpdate.Data.Single(b => b.UserId == user2Id);
        Assert.Equal(0m, user2AfterUpdate.TotalPaid);
        Assert.Equal(100m, user2AfterUpdate.TotalOwed);
        Assert.Equal(-100m, user2AfterUpdate.Balance);
    }

    #endregion

    #region Member write invalidation

    [Fact]
    public async Task GetBalances_AddMember_InvalidatesCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();

        // Warm the cache with a single-member balance list
        var warm = await GetBalancesAsync(client, group.Id);
        Assert.Single(warm.Data!);

        // Add a second member — must invalidate the cached balances
        await SeedSecondMemberAsync(client, group.Id);

        var afterAdd = await GetBalancesAsync(client, group.Id);
        Assert.Equal(2, afterAdd.Data!.Count);
        Assert.All(afterAdd.Data, b => Assert.Equal(0m, b.Balance));
    }

    [Fact]
    public async Task GetBalances_RemoveMember_InvalidatesCache()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();
        var group = await client.CreateGroupAsync();
        var (_, user2Id, _) = await SeedSecondMemberAsync(client, group.Id);

        // Warm the cache with a two-member balance list
        var warm = await GetBalancesAsync(client, group.Id);
        Assert.Equal(2, warm.Data!.Count);

        // Remove the second member — must invalidate the cached balances
        var deleteResponse = await client.DeleteAsync(
            $"/api/v1/groups/{group.Id}/members/{user2Id}", ct);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var afterRemove = await GetBalancesAsync(client, group.Id);
        Assert.Single(afterRemove.Data!);
        Assert.Equal(0m, afterRemove.Data[0].Balance);
    }

    #endregion
}
