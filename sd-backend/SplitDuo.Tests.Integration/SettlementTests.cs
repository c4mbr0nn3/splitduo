using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Categories.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.ExpenseAttachments.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Groups.Dto;
using SplitDuo.Api.Features.Settlements.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class SettlementTests : IntegrationTest
{
    public SettlementTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private static object SettlementPayload(string fromUserId, string? toUserId, decimal amount,
        string date = "2025-01-20", string? description = null, int? paymentModeId = null)
    {
        return new
        {
            fromUserId,
            toUserId,
            amount,
            date,
            description,
            paymentModeId
        };
    }

    private async Task<List<BalanceDto>> GetBalancesAsync(HttpClient client, string groupId)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.GetAsync($"/api/v1/groups/{groupId}/balances", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<BalanceDto>>>(ct);
        return body!.Data!;
    }

    #endregion

    #region Create settlement — balance netting

    [Fact]
    public async Task CreateSettlement_Individual_NetsBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Admin pays 100, split 50/50 → admin +50, user2 -50
        await adminClient.CreateExpenseAsync(groupId, adminId, amount: 100m,
            splits: new[]
            {
                new { userId = adminId, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // User2 settles their 50 debt by paying admin 50
        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 50m));

        Assert.Equal(user2Id, settlement.FromUserId);
        Assert.Equal(adminId, settlement.ToUserId);
        Assert.Equal(50m, settlement.Amount);
        Assert.Equal(1, settlement.ExpenseTypeId);
        Assert.Equal(4, settlement.PaymentModeId);

        var balances = await GetBalancesAsync(adminClient, groupId);
        Assert.Equal(0m, balances.Single(b => b.UserId == adminId).Balance);
        Assert.Equal(0m, balances.Single(b => b.UserId == user2Id).Balance);
    }

    [Fact]
    public async Task CreateSettlement_PartialAmount_ReducesBalancePartially()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Admin pays 100, split 50/50 → admin +50, user2 -50
        await adminClient.CreateExpenseAsync(groupId, adminId, amount: 100m,
            splits: new[]
            {
                new { userId = adminId, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // User2 pays back only 20
        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 20m));

        var balances = await GetBalancesAsync(adminClient, groupId);

        var adminBalance = balances.Single(b => b.UserId == adminId);
        Assert.Equal(100m, adminBalance.TotalPaid);
        Assert.Equal(70m, adminBalance.TotalOwed);
        Assert.Equal(30m, adminBalance.Balance);

        var user2Balance = balances.Single(b => b.UserId == user2Id);
        Assert.Equal(-30m, user2Balance.Balance);
    }

    [Fact]
    public async Task CreateSettlement_NonSuggestedPair_Accepted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // No expenses — settle 15 anyway
        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 15m));

        var balances = await GetBalancesAsync(adminClient, groupId);
        Assert.Equal(15m, balances.Single(b => b.UserId == user2Id).Balance);
        Assert.Equal(-15m, balances.Single(b => b.UserId == adminId).Balance);
    }

    #endregion

    #region Stats exclusion

    [Fact]
    public async Task GetGroupStats_ExcludesSettlements()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        await adminClient.CreateExpenseAsync(groupId, adminId, amount: 50m,
            categoryId: 2, expenseDate: "2025-01-15");

        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 30m, date: "2025-01-16"));

        var response = await adminClient.GetAsync($"/api/v1/groups/{groupId}/stats", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupStatsDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(1, body.Data!.ExpenseCount);
        Assert.Equal(50m, body.Data.TotalAmount);
        var category = Assert.Single(body.Data.CategoryBreakdown);
        Assert.Equal(2, category.CategoryId);
        Assert.Equal(50m, category.Amount);
        var monthly = Assert.Single(body.Data.MonthlyBreakdown);
        Assert.Equal(2025, monthly.Year);
        Assert.Equal(1, monthly.Month);
        Assert.Equal(50m, monthly.Amount);
    }

    #endregion

    #region Expense list / guards

    [Fact]
    public async Task ExpenseList_ReturnsSettlementsWithExpenseTypeId()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var expense = await adminClient.CreateExpenseAsync(groupId, adminId, amount: 40m,
            splits: new[]
            {
                new { userId = adminId, splitAmount = 20m },
                new { userId = user2Id, splitAmount = 20m },
            });

        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 20m));

        var response = await adminClient.GetAsync($"/api/v1/groups/{groupId}/expenses", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(2, body.Data!.Count);

        var settlementDto = body.Data.Single(e => e.Amount == 20m && e.Id == settlement.Id);
        Assert.Equal(1, settlementDto.ExpenseTypeId);
        Assert.Equal(12, settlementDto.CategoryId);

        var expenseDto = body.Data.Single(e => e.Id == expense.Id);
        Assert.Equal(0, expenseDto.ExpenseTypeId);
    }

    [Fact]
    public async Task CreateExpense_WithSettlementCategory_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, _) = await SetupGroupWithTwoMembersAsync();

        var response = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/expenses", new
        {
            title = "Cheating",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 12,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("The Settlement category is reserved for settlements and cannot be used for expenses",
            body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateExpense_WithSettlementCategory_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var expense = await adminClient.CreateExpenseAsync(groupId, adminId, amount: 40m,
            splits: new[]
            {
                new { userId = adminId, splitAmount = 20m },
                new { userId = user2Id, splitAmount = 20m },
            });

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/expenses/{expense.Id}", new { categoryId = 12 }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("The Settlement category is reserved for settlements and cannot be used for expenses",
            body!.Error!.Message);
    }

    [Fact]
    public async Task UpdateExpense_OnSettlement_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 10m));

        var response = await adminClient.PutAsJsonAsync(
            $"/api/v1/groups/{groupId}/expenses/{settlement.Id}", new { title = "x" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Settlements cannot be updated; delete and recreate the settlement instead",
            body!.Error!.Message);
    }

    #endregion

    #region Categories

    [Fact]
    public async Task GetCategories_ExcludesSettlement()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/categories", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CategoryDto>>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(11, body.Data!.Count);
        Assert.DoesNotContain(body.Data, c => c.Id == 12);
    }

    #endregion

    #region Attachment guard

    [Fact]
    public async Task UploadAttachment_OnSettlement_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 10m));

        var response = await adminClient.UploadAttachmentAsync(
            groupId, settlement.Id, AttachmentTestExtensions.JpegBytes, "test.jpg");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseAttachmentDto>>(ct);
        Assert.Equal("Attachments cannot be added to settlements", body!.Error!.Message);
    }

    #endregion

    #region Delete settlement

    [Fact]
    public async Task DeleteSettlement_ReversesBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Admin pays 100, split 50/50 → admin +50, user2 -50
        await adminClient.CreateExpenseAsync(groupId, adminId, amount: 100m,
            splits: new[]
            {
                new { userId = adminId, splitAmount = 50m },
                new { userId = user2Id, splitAmount = 50m },
            });

        // Settle 50 → both 0
        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 50m));

        var afterSettle = await GetBalancesAsync(adminClient, groupId);
        Assert.Equal(0m, afterSettle.Single(b => b.UserId == adminId).Balance);
        Assert.Equal(0m, afterSettle.Single(b => b.UserId == user2Id).Balance);

        var deleteResponse = await adminClient.DeleteAsync(
            $"/api/v1/groups/{groupId}/settlements/{settlement.Id}", ct);
        Assert.True(deleteResponse.IsSuccessStatusCode);

        var getResponse = await adminClient.GetAsync(
            $"/api/v1/groups/{groupId}/settlements/{settlement.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var afterDelete = await GetBalancesAsync(adminClient, groupId);
        Assert.Equal(50m, afterDelete.Single(b => b.UserId == adminId).Balance);
        Assert.Equal(-50m, afterDelete.Single(b => b.UserId == user2Id).Balance);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task CreateSettlement_Validation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Amount zero → model validation (Range attribute) rejects before the service guard
        var zero = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, 0m), ct);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);

        // Amount negative → model validation
        var negative = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, -5m), ct);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        // From user id not a guid
        var badFrom = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload("not-a-guid", adminId, 10m), ct);
        Assert.Equal(HttpStatusCode.BadRequest, badFrom.StatusCode);
        var badFromBody = await badFrom.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("From user not found", badFromBody!.Error!.Message);

        // Invalid payment mode
        var badMode = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, 10m, paymentModeId: 99), ct);
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);
        var badModeBody = await badMode.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("Invalid expense payment mode", badModeBody!.Error!.Message);

        // Invalid date
        var badDate = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, 10m, date: "20-01-2025"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
        var badDateBody = await badDate.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("Invalid settlement date format", badDateBody!.Error!.Message);

        // Unauthenticated → 401 before any service-level validation
        var unauth = await Client.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, 10m), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // Non-member caller (403)
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider-st@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme123");
        var forbidden = await outsiderClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, adminId, 10m), ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var forbiddenBody = await forbidden.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("Access to this group is not allowed", forbiddenBody!.Error!.Message);

        // Missing date → model validation 400
        var missingDate = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            new { fromUserId = user2Id, toUserId = adminId, amount = 10m }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingDate.StatusCode);

        // From user valid guid but not a member
        var nonMemberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "nonmember-st@localhost");
        var nonMemberClient = await CreateAuthenticatedClientAsync(nonMemberEmail, "changeme123");
        var nonMemberId = (await nonMemberClient.GetCurrentUserAsync()).Id;

        var fromNonMember = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(nonMemberId, adminId, 10m), ct);
        Assert.Equal(HttpStatusCode.BadRequest, fromNonMember.StatusCode);
        var fromNonMemberBody = await fromNonMember.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("From user is not a member of this group", fromNonMemberBody!.Error!.Message);

        var toNonMember = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements",
            SettlementPayload(user2Id, nonMemberId, 10m), ct);
        Assert.Equal(HttpStatusCode.BadRequest, toNonMember.StatusCode);
        var toNonMemberBody = await toNonMember.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("To user is not a member of this group", toNonMemberBody!.Error!.Message);
    }

    #endregion

    #region Alias mode

    [Fact]
    public async Task CreateSettlement_AliasMode_SetsAliasSplitsAndMovesAliasBalances()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId, coupleAliasId) =
            await SetupFinalizedAliasGroupAsync();

        // The finalized-alias setup assigns both members to the multi-person "Couple" alias,
        // so each member's GroupMember.AliasId IS the Couple alias — the payer settles from
        // their current alias (Couple), the receiver is user2's singleton.
        var settlement = await adminClient.CreateSettlementAsync(groupId, new
        {
            fromUserId = adminId,
            toUserId = (string?)null,
            fromAliasId = coupleAliasId,
            toAliasId = user2SingletonAliasId,
            amount = 25m,
            date = "2025-01-20",
        });

        Assert.Null(settlement.ToUserId);
        Assert.Equal(coupleAliasId, settlement.PaidByAliasId);
        Assert.Equal(user2SingletonAliasId, settlement.ToAliasId);
        Assert.NotNull(settlement.PaidByAliasName);
        Assert.NotNull(settlement.ToAliasName);
        Assert.NotEqual(settlement.PaidByAliasName, settlement.ToAliasName);

        // Alias balances must have moved by ±25
        var response = await adminClient.GetAsync($"/api/v1/groups/{groupId}/balances", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasBalanceDto>>>(ct);
        Assert.NotNull(body!.Data);

        var fromAlias = body.Data.Single(b => b.AliasId == coupleAliasId);
        var toAlias = body.Data.Single(b => b.AliasId == user2SingletonAliasId);
        Assert.Equal(25m, fromAlias.Balance);
        Assert.Equal(-25m, toAlias.Balance);
    }

    private async Task<(HttpClient adminClient, string groupId, string adminId, string user2Id,
        string adminSingletonAliasId, string user2SingletonAliasId, string coupleAliasId)>
        SetupFinalizedAliasGroupAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: true);
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "u2@localhost", "changeme123", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var user2Client = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await user2Client.GetCurrentUserAsync();

        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var adminSingletonId = listBody!.Data!.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == admin.Id)).Id;
        var user2SingletonId = listBody.Data.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == user2.Id)).Id;

        var aliasResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases", new { name = "Couple" }, ct);
        aliasResponse.EnsureSuccessStatusCode();
        var aliasBody = await aliasResponse.Content.ReadFromJsonAsync<ApiResponseDto<AliasDto>>(ct);
        var coupleAliasId = aliasBody!.Data!.Id;

        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{coupleAliasId}/members", new { userId = admin.Id }, ct);
        await adminClient.PostAsJsonAsync(
            $"/api/v1/aliases/{coupleAliasId}/members", new { userId = user2.Id }, ct);

        var finalizeResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/aliases/finalize", new { }, ct);
        finalizeResponse.EnsureSuccessStatusCode();

        return (adminClient, group.Id, admin.Id, user2.Id, adminSingletonId, user2SingletonId, coupleAliasId);
    }

    #endregion

    #region CSV export / import round trip

    [Fact]
    public async Task CsvExport_Import_RoundTripsSettlement()
    {
        var ct = TestContext.Current.CancellationToken;
        // Unique emails (not the shared admin@splitduo.local/u2@localhost) so the fresh
        // import-group setup can seed users with the same emails without conflicts.
        var adminEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "csv-admin@localhost", "changeme123", "Admin", "User");
        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "csv-u2@localhost", "changeme123", "Second", "User");
        var adminClient = await CreateAuthenticatedClientAsync(adminEmail, "changeme123");
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var user2Client = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await user2Client.GetCurrentUserAsync();

        var groupId = group.Id;
        var adminId = admin.Id;
        var user2Id = user2.Id;

        await adminClient.CreateExpenseAsync(groupId, adminId, amount: 30m, categoryId: 11,
            expenseDate: "2025-01-15",
            splits: new[]
            {
                new { userId = adminId, splitAmount = 15m },
                new { userId = user2Id, splitAmount = 15m },
            });
        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 15m, date: "2025-01-20", description: "Square up"));

        var exportResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}/export/csv", ct);
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        var csv = await exportResponse.Content.ReadAsStringAsync(ct);

        Assert.Contains("Settlement", csv);

        // Fresh group with distinct emails; rewrite the CSV emails to match the fresh group
        var (adminClient2, groupId2, adminId2, user2Id2) = await SetupFreshTwoMemberGroupAsync();
        var roundTripCsv = csv.Replace("csv-admin@localhost", "import-admin@localhost")
            .Replace("csv-u2@localhost", "import-u2@localhost");

        var analyzedImportId = await AnalyzeAndMapAsync(adminClient2, groupId2, roundTripCsv, new ImportMappingDto
        {
            UserMappings = new()
            {
                ["import-admin@localhost"] = adminId2,
                ["import-u2@localhost"] = user2Id2,
            },
        });

        var import = await RunJobAndGetStatusAsync(adminClient2, groupId2, analyzedImportId);
        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);

        var expenses = await GetExpensesAsync(adminClient2, groupId2);
        Assert.Equal(2, expenses.Count);

        var importedSettlement = expenses.Single(e => e.ExpenseTypeId == 1);
        Assert.Equal(12, importedSettlement.CategoryId);
        Assert.Equal(15m, importedSettlement.Amount);

        var importedExpense = expenses.Single(e => e.ExpenseTypeId == 0);
        Assert.Equal(30m, importedExpense.Amount);
    }

    private async Task<(HttpClient adminClient, string groupId, string adminId, string user2Id)>
        SetupFreshTwoMemberGroupAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "import-admin@localhost", "changeme123", "Import", "Admin");
        var adminClient = await CreateAuthenticatedClientAsync(adminEmail, "changeme123");
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "import-u2@localhost", "changeme123", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var user2Client = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await user2Client.GetCurrentUserAsync();

        return (adminClient, group.Id, admin.Id, user2.Id);
    }

    private static ImportMappingDto CreateMapping(string adminId, string? user2Id = null)
    {
        var mapping = new ImportMappingDto
        {
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
            },
        };
        if (user2Id != null)
        {
            mapping.UserMappings["u2@localhost"] = user2Id;
        }
        return mapping;
    }

    private async Task<string> AnalyzeAndMapAsync(
        HttpClient adminClient, string groupId, string csv, ImportMappingDto mapping)
    {
        var ct = TestContext.Current.CancellationToken;
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, csv);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        mapping.ImportId = importId;
        var response = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/imports", mapping, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return importId;
    }

    private async Task<ImportStatusDto> RunJobAndGetStatusAsync(
        HttpClient adminClient, string groupId, string importId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Factory.Services.CreateScope();
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuo);

        var importsResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}/imports", ct);
        Assert.Equal(HttpStatusCode.OK, importsResponse.StatusCode);
        var importsBody = await importsResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ImportStatusDto>>(ct);
        return importsBody!.Data!.Single(i => i.Id == importId);
    }

    private async Task<List<ExpenseDto>> GetExpensesAsync(HttpClient adminClient, string groupId)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await adminClient.GetAsync($"/api/v1/groups/{groupId}/expenses", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        return body!.Data!;
    }

    #endregion

    #region Get by id

    [Fact]
    public async Task GetSettlement_ById()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var settlement = await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 10m));

        var ok = await adminClient.GetAsync($"/api/v1/groups/{groupId}/settlements/{settlement.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var okBody = await ok.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal(settlement.Id, okBody!.Data!.Id);

        var missing = await adminClient.GetAsync(
            $"/api/v1/groups/{groupId}/settlements/{Guid.NewGuid()}", ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingBody = await missing.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("Settlement not found", missingBody!.Error!.Message);

        var invalid = await adminClient.GetAsync($"/api/v1/groups/{groupId}/settlements/not-a-guid", ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidBody = await invalid.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        Assert.Equal("Invalid settlement ID format", invalidBody!.Error!.Message);
    }

    #endregion

    #region List pagination

    [Fact]
    public async Task GetGroupSettlements_PaginatedNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 10m, date: "2025-01-01"));
        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 20m, date: "2025-02-01"));
        await adminClient.CreateSettlementAsync(groupId,
            SettlementPayload(user2Id, adminId, 30m, date: "2025-03-01"));

        var response = await adminClient.GetAsync(
            $"/api/v1/groups/{groupId}/settlements?page=1&limit=2", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<SettlementDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(2, body.Data!.Count);

        Assert.Equal(3, body.Pagination!.Total);
        Assert.Equal(2, body.Pagination!.TotalPages);
        Assert.True(body.Pagination!.HasNext);
        Assert.False(body.Pagination!.HasPrev);

        // First item is the newest by date
        Assert.Equal(30m, body.Data[0].Amount);
        Assert.Equal("2025-03-01", body.Data[0].Date);
    }

    #endregion
}