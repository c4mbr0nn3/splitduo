using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class CospendImportsTests : IntegrationTest
{
    public CospendImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private const int CospendImportTypeId = (int)ImportType.Cospend;

    /// <summary>
    /// Cospend CSV with members, categories, payment modes and one expense (30.00, payer Alice,
    /// owers Alice,Bob). The owers field is quoted because it contains commas.
    /// </summary>
    private static readonly string CospendCsv = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        Dinner,30.00,2026-01-01,1767225600,Alice,1,1,"Alice,Bob",0,0,0,,1,c,1,,0
        """;

    /// <summary>
    /// Cospend CSV with members, categories and payment modes sections only (no expenses).
    /// </summary>
    private static readonly string CospendCsvNoExpenses = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000
        """;

    /// <summary>
    /// Cospend CSV with one 10.00 expense split across three owers (Alice, Bob, admin).
    /// </summary>
    private static readonly string CospendCsvThreeOwers = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        Dinner,10.00,2026-01-01,1767225600,Alice,1,1,"Alice,Bob,admin",0,0,0,,1,c,1,,0
        """;

    /// <summary>
    /// Cospend CSV with two expenses — the second one has deleted=1 and must be skipped.
    /// </summary>
    private static readonly string CospendCsvWithDeletedExpense = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        Dinner,30.00,2026-01-01,1767225600,Alice,1,1,"Alice,Bob",0,0,0,,1,c,1,,0
        Lunch,20.00,2026-01-02,1767312000,Alice,1,1,"Alice,Bob",0,0,0,,1,c,1,,1
        """;

    /// <summary>
    /// Cospend CSV whose payer name (Charlie) is not present in the user mappings.
    /// </summary>
    private static readonly string CospendCsvUnknownPayer = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        Dinner,30.00,2026-01-01,1767225600,Charlie,1,1,"Alice,Bob",0,0,0,,1,c,1,,0
        """;

    /// <summary>
    /// Cospend CSV whose owers (Charlie, David) are not in the user mappings.
    /// </summary>
    private static readonly string CospendCsvUnknownOwers = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        Dinner,30.00,2026-01-01,1767225600,Alice,1,1,"Charlie,David",0,0,0,,1,c,1,,0
        """;

    /// <summary>
    /// Cospend CSV with an empty expenses section (header only).
    /// </summary>
    private static readonly string CospendCsvEmptyExpenses = """
        name,weight,active,color
        Alice,1,1,#ff0000
        Bob,1,1,#00ff00

        categoryname,categoryid,icon,color
        Food,1,utensils,#ff0000

        paymentmodename,paymentmodeid,icon,color
        Cash,1,,#000000

        what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
        """;

    private static ImportMappingDto CreateMapping(string adminId, string user2Id,
        Dictionary<string, string>? extraUserMappings = null)
    {
        var userMappings = new Dictionary<string, string>
        {
            ["Alice"] = adminId,
            ["Bob"] = user2Id,
        };
        if (extraUserMappings != null)
        {
            foreach (var (key, value) in extraUserMappings) userMappings[key] = value;
        }

        return new ImportMappingDto
        {
            UserMappings = userMappings,
            CategoryMappings = new() { [1] = (int)ExpenseCategory.Dining },
            PaymentModeMappings = new() { [1] = (int)PaymentMode.Cash },
        };
    }

    /// <summary>
    /// Analyzes the CSV as a Cospend import, then posts the mapping to trigger the import job.
    /// Returns the import GUID.
    /// </summary>
    private async Task<string> AnalyzeAndMapAsync(
        HttpClient adminClient, string groupId, string csv, ImportMappingDto mapping)
    {
        var ct = TestContext.Current.CancellationToken;
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, csv, CospendImportTypeId);
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
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.Cospend);

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
    public async Task Analyze_CospendCsv_ReturnsMembersCategoriesPaymentModes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, CospendCsvNoExpenses, CospendImportTypeId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal((int)ImportType.Cospend, body.Data!.ImportTypeId);
        Assert.Equal((int)ImportStatus.Pending, body.Data.ImportStatusId);

        // AnalysisResults JSON contains the extracted member/category/payment-mode names
        Assert.NotNull(body.Data.AnalysisResults);
        Assert.Contains("Alice", body.Data.AnalysisResults!);
        Assert.Contains("Bob", body.Data.AnalysisResults);
        Assert.Contains("Food", body.Data.AnalysisResults);
        Assert.Contains("Cash", body.Data.AnalysisResults);
    }

    #endregion

    #region Process — happy path

    [Fact]
    public async Task Process_CospendCsv_CreatesExpensesWithEqualSplits()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsv,
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
        Assert.Equal((int)PaymentMode.Cash, expense.PaymentModeId);
        Assert.Equal(2, expense.Splits.Count);
        Assert.All(expense.Splits, split => Assert.Equal(15.00m, split.SplitAmount));
    }

    [Fact]
    public async Task Process_EqualSplits_RoundingDifference_AdjustedOnLastSplit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsvThreeOwers,
            CreateMapping(adminId, user2Id, new() { ["admin"] = adminId }));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal(3, expense.Splits.Count);
        Assert.Equal(10.00m, expense.Splits.Sum(s => s.SplitAmount));
        Assert.Equal(2, expense.Splits.Count(s => s.SplitAmount == 3.33m));
        Assert.Equal(1, expense.Splits.Count(s => s.SplitAmount == 3.34m));
    }

    [Fact]
    public async Task Process_DeletedExpenses_Skipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsvWithDeletedExpense,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal("Dinner", expense.Title);
    }

    #endregion

    #region Process — skipped expenses

    [Fact]
    public async Task Process_PayerNotInMapping_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsvUnknownPayer,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    [Fact]
    public async Task Process_NoValidOwers_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsvUnknownOwers,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    [Fact]
    public async Task Process_NoValidExpenses_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, CospendCsvEmptyExpenses,
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

        var importId = await AnalyzeAndMapAsync(adminClient, group.Id, CospendCsv,
            CreateMapping(admin.Id, user2.Id));

        var import = await RunJobAndGetStatusAsync(adminClient, group.Id, importId);

        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("alias mode", import.ErrorDetails);
    }

    #endregion
}
