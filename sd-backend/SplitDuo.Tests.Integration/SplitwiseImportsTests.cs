using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class SplitwiseImportsTests : IntegrationTest
{
    public SplitwiseImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private const int SplitwiseImportTypeId = (int)ImportType.Splitwise;

    /// <summary>
    /// Splitwise CSV with one expense (cost 30.00, payer Alice +30.00, ower Bob -20.00).
    /// Payer share = 30.00 - 20.00 = 10.00.
    /// </summary>
    private static readonly string SplitwiseCsv = """
        Date,Description,Category,Cost,Currency,Alice,Bob
        2026-01-01,Dinner,Food,30.00,EUR,30.00,-20.00
        """;

    /// <summary>
    /// Splitwise CSV with one expense fully covered by the ower (cost 30.00, payer Alice +30.00,
    /// ower Bob -30.00). Payer share = 0 → no payer split.
    /// </summary>
    private static readonly string SplitwiseCsvPayerShareZero = """
        Date,Description,Category,Cost,Currency,Alice,Bob
        2026-01-01,Dinner,Food,30.00,EUR,30.00,-30.00
        """;

    /// <summary>
    /// Splitwise CSV with two positive participant values (multiple payers) — the parser skips the row.
    /// </summary>
    private static readonly string SplitwiseCsvMultiplePayers = """
        Date,Description,Category,Cost,Currency,Alice,Bob
        2026-01-01,Dinner,Food,30.00,EUR,30.00,10.00
        """;

    /// <summary>
    /// Splitwise CSV whose payer (Charlie) is not present in the user mappings.
    /// </summary>
    private static readonly string SplitwiseCsvUnknownPayer = """
        Date,Description,Category,Cost,Currency,Charlie,Bob
        2026-01-01,Dinner,Food,30.00,EUR,30.00,-20.00
        """;

    private static ImportMappingDto CreateMapping(string adminId, string user2Id)
    {
        return new ImportMappingDto
        {
            UserMappings = new()
            {
                ["Alice"] = adminId,
                ["Bob"] = user2Id,
            },
            CategoryMappings = new() { [0] = (int)ExpenseCategory.Dining },
        };
    }

    /// <summary>
    /// Analyzes the CSV as a Splitwise import, then posts the mapping to trigger the import job.
    /// Returns the import GUID.
    /// </summary>
    private async Task<string> AnalyzeAndMapAsync(
        HttpClient adminClient, string groupId, string csv, ImportMappingDto mapping)
    {
        var ct = TestContext.Current.CancellationToken;
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, csv, SplitwiseImportTypeId);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        mapping.ImportId = importId;
        var response = await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/imports", mapping, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return importId;
    }

    /// <summary>
    /// Runs the import job directly (bypassing the Quartz scheduler) and returns the import status.
    /// </summary>
    private async Task<ImportStatusDto> RunJobAndGetStatusAsync(
        HttpClient adminClient, string groupId, string importId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Factory.Services.CreateScope();
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.Splitwise);

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

    #region Analyze

    [Fact]
    public async Task Analyze_SplitwiseCsv_ReturnsMembersAndCategories()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, SplitwiseCsv, SplitwiseImportTypeId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal((int)ImportType.Splitwise, body.Data!.ImportTypeId);
        Assert.Equal((int)ImportStatus.Pending, body.Data.ImportStatusId);

        // AnalysisResults JSON contains the extracted participant and category names
        Assert.NotNull(body.Data.AnalysisResults);
        Assert.Contains("Alice", body.Data.AnalysisResults!);
        Assert.Contains("Bob", body.Data.AnalysisResults);
        Assert.Contains("Food", body.Data.AnalysisResults);
    }

    #endregion

    #region Process — happy path

    [Fact]
    public async Task Process_SplitwiseCsv_CreatesExpensesWithPayerShare()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitwiseCsv,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal("Dinner", expense.Title);
        Assert.Equal(30.00m, expense.Amount);
        Assert.Equal(adminId, expense.PaidByUserId);
        Assert.Equal((int)ExpenseCategory.Dining, expense.CategoryId);
        Assert.Equal((int)PaymentMode.Other, expense.PaymentModeId);

        // 2 splits: ower Bob 20.00, payer share Alice 10.00
        Assert.Equal(2, expense.Splits.Count);
        var bobSplit = Assert.Single(expense.Splits, s => s.UserId == user2Id);
        Assert.Equal(20.00m, bobSplit.SplitAmount);
        var aliceSplit = Assert.Single(expense.Splits, s => s.UserId == adminId);
        Assert.Equal(10.00m, aliceSplit.SplitAmount);
    }

    [Fact]
    public async Task Process_PayerShareZero_NoPayerSplit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitwiseCsvPayerShareZero,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal(30.00m, expense.Amount);

        // Payer share = 30.00 - 30.00 = 0 → only the ower split exists
        var split = Assert.Single(expense.Splits);
        Assert.Equal(user2Id, split.UserId);
        Assert.Equal(30.00m, split.SplitAmount);
    }

    #endregion

    #region Process — skipped rows

    [Fact]
    public async Task Process_MultiplePayers_SkipsRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitwiseCsvMultiplePayers,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    [Fact]
    public async Task Process_PayerNotInMapping_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitwiseCsvUnknownPayer,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    #endregion

    #region Process — alias mode

    [Fact]
    public async Task Process_AliasModeGroup_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync(useAliases: true);
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "u2@localhost");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme123");
        var user2 = await memberClient.GetCurrentUserAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, group.Id, SplitwiseCsv,
            CreateMapping(admin.Id, user2.Id));

        var import = await RunJobAndGetStatusAsync(adminClient, group.Id, importId);

        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("alias mode", import.ErrorDetails);
    }

    #endregion
}
