using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Aliases.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class SplitDuoAliasImportsTests : IntegrationTest
{
    public SplitDuoAliasImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private const int SplitDuoAliasImportTypeId = (int)ImportType.SplitDuoAlias;

    /// <summary>
    /// SplitDuoAlias CSV with aliases, members and one expense (42.50, paid by admin,
    /// alias splits Alice:21.25|Bob:21.25).
    /// </summary>
    private static readonly string SplitDuoAliasCsv = """
        name,is_singleton
        Alice,0
        Bob,0

        email,alias_name,role
        admin@splitduo.local,Alice,admin
        u2@localhost,Bob,member

        date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
        2026-01-01,Dinner,Team dinner,42.50,admin@splitduo.local,Alice,Dining,Cash,Alice:21.25|Bob:21.25
        """;

    /// <summary>
    /// SplitDuoAlias CSV with aliases and members sections only (no expenses).
    /// </summary>
    private static readonly string SplitDuoAliasCsvNoExpenses = """
        name,is_singleton
        Alice,0
        Bob,0

        email,alias_name,role
        admin@splitduo.local,Alice,admin
        u2@localhost,Bob,member
        """;

    /// <summary>
    /// SplitDuoAlias CSV whose alias splits (30.00 + 20.00 = 50.00) do not sum up to the
    /// expense amount (42.50) — the expense must be skipped.
    /// </summary>
    private static readonly string SplitDuoAliasCsvSplitMismatch = """
        name,is_singleton
        Alice,0
        Bob,0

        email,alias_name,role
        admin@splitduo.local,Alice,admin
        u2@localhost,Bob,member

        date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
        2026-01-01,Dinner,Team dinner,42.50,admin@splitduo.local,Alice,Dining,Cash,Alice:30.00|Bob:20.00
        """;

    /// <summary>
    /// SplitDuoAlias CSV whose alias splits (Charlie, David) are not present in the alias mappings.
    /// </summary>
    private static readonly string SplitDuoAliasCsvUnknownAlias = """
        name,is_singleton
        Alice,0
        Bob,0

        email,alias_name,role
        admin@splitduo.local,Alice,admin
        u2@localhost,Bob,member

        date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
        2026-01-01,Dinner,Team dinner,42.50,admin@splitduo.local,Alice,Dining,Cash,Charlie:21.25|David:21.25
        """;

    private static ImportMappingDto CreateMapping(string adminId, string user2Id,
        string? adminAliasId = null, string? user2AliasId = null)
    {
        var mapping = new ImportMappingDto
        {
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = user2Id,
            },
        };

        if (adminAliasId != null && user2AliasId != null)
        {
            mapping.AliasMappings = new()
            {
                ["Alice"] = adminAliasId,
                ["Bob"] = user2AliasId,
            };
        }

        return mapping;
    }

    /// <summary>
    /// Creates an alias-mode group with two members, a multi-person alias containing both,
    /// and finalizes alias setup. Returns the admin client, group id, admin/user2 ids, and
    /// the ids of the "Couple" alias and both members' singleton aliases.
    /// </summary>
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

        // Capture the singleton alias ids while they still have members (they become
        // empty once both members are reassigned to the multi-person alias below).
        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var adminSingletonId = listBody!.Data!.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == admin.Id)).Id;
        var user2SingletonId = listBody.Data.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == user2.Id)).Id;

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

        return (adminClient, group.Id, admin.Id, user2.Id, adminSingletonId, user2SingletonId, coupleAliasId);
    }

    /// <summary>
    /// Analyzes the CSV as a SplitDuoAlias import, then posts the mapping to trigger the import job.
    /// Returns the import GUID.
    /// </summary>
    private async Task<string> AnalyzeAndMapAsync(
        HttpClient adminClient, string groupId, string csv, ImportMappingDto mapping)
    {
        var ct = TestContext.Current.CancellationToken;
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, csv, SplitDuoAliasImportTypeId);
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
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuoAlias);

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
    public async Task Analyze_SplitDuoAliasCsv_ReturnsMembersAndAliases()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(
            adminClient, groupId, SplitDuoAliasCsvNoExpenses, SplitDuoAliasImportTypeId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal((int)ImportType.SplitDuoAlias, body.Data!.ImportTypeId);
        Assert.Equal((int)ImportStatus.Pending, body.Data.ImportStatusId);

        // AnalysisResults JSON contains the extracted member emails and alias names
        Assert.NotNull(body.Data.AnalysisResults);
        Assert.Contains("admin@splitduo.local", body.Data.AnalysisResults!);
        Assert.Contains("u2@localhost", body.Data.AnalysisResults);
        Assert.Contains("Alice", body.Data.AnalysisResults);
        Assert.Contains("Bob", body.Data.AnalysisResults);
    }

    #endregion

    #region Process — happy path

    [Fact]
    public async Task Process_SplitDuoAliasCsv_CreatesExpensesWithAliasSplits()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId, _) =
            await SetupFinalizedAliasGroupAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoAliasCsv,
            CreateMapping(adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal("Dinner", expense.Title);
        Assert.Equal(42.50m, expense.Amount);
        Assert.Equal(adminId, expense.PaidByUserId);
        Assert.Equal((int)ExpenseCategory.Dining, expense.CategoryId);
        Assert.Equal((int)PaymentMode.Cash, expense.PaymentModeId);

        // 2 alias splits of 21.25 each
        Assert.Equal(2, expense.AliasSplits!.Count);
        Assert.All(expense.AliasSplits, split => Assert.Equal(21.25m, split.SplitAmount));
    }

    #endregion

    #region Process — conflicts

    [Fact]
    public async Task Process_NonAliasModeGroup_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoAliasCsv,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("not in alias mode", import.ErrorDetails);
    }

    [Fact]
    public async Task Process_AliasSetupNotFinalized_ReturnsConflict()
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

        // Capture the singleton alias ids (alias setup is NOT finalized)
        var list = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/aliases", ct);
        var listBody = await list.Content.ReadFromJsonAsync<ApiResponseDto<List<AliasDto>>>(ct);
        var adminSingletonId = listBody!.Data!.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == admin.Id)).Id;
        var user2SingletonId = listBody.Data.Single(a => a.IsSingleton && a.Members.Any(m => m.Id == user2.Id)).Id;

        var importId = await AnalyzeAndMapAsync(adminClient, group.Id, SplitDuoAliasCsv,
            CreateMapping(admin.Id, user2.Id, adminSingletonId, user2SingletonId));

        var import = await RunJobAndGetStatusAsync(adminClient, group.Id, importId);

        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("finalized", import.ErrorDetails);
    }

    #endregion

    #region Process — skipped expenses

    [Fact]
    public async Task Process_SplitSumMismatch_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId, _) =
            await SetupFinalizedAliasGroupAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoAliasCsvSplitMismatch,
            CreateMapping(adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    [Fact]
    public async Task Process_AliasNotInMapping_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId, _) =
            await SetupFinalizedAliasGroupAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoAliasCsvUnknownAlias,
            CreateMapping(adminId, user2Id, adminSingletonAliasId, user2SingletonAliasId));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    #endregion
}
