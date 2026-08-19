using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class ImportsTests : IntegrationTest
{
    public ImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private static readonly string SplitDuoCsv = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,Dining,Cash,admin@splitduo.local:15.00|u2@localhost:15.00
        2025-02-01,Bus,Trip,10.00,u2@localhost,Transportation,Card,u2@localhost:5.00|admin@splitduo.local:5.00
        """;

    #endregion

    #region Analyze — happy path

    [Fact]
    public async Task Analyze_SplitDuoCsv_ReturnsAnalysisWithMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.NotEmpty(body.Data!.Id);
        Assert.Equal((int)ImportType.SplitDuo, body.Data.ImportTypeId);
        Assert.Equal((int)ImportStatus.Pending, body.Data.ImportStatusId);
        Assert.NotEmpty(body.Data.FileHash);
        Assert.Equal("import.csv", body.Data.FileName);

        // AnalysisResults JSON contains the extracted members
        Assert.NotNull(body.Data.AnalysisResults);
        Assert.Contains("admin@splitduo.local", body.Data.AnalysisResults!);
        Assert.Contains("u2@localhost", body.Data.AnalysisResults);
    }

    [Fact]
    public async Task Analyze_CreatesImportRecord_InList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

        var listResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}/imports", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ImportStatusDto>>(ct);
        Assert.Single(listBody!.Data!);
        Assert.Equal("import.csv", listBody.Data[0].FileName);
    }

    #endregion

    #region Analyze — validation

    [Fact]
    public async Task Analyze_NonCsvFile_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("hello"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "import.txt");
        content.Add(new StringContent(((int)ImportType.SplitDuo).ToString()), "ImportTypeId");

        var response = await adminClient.PostAsync(
            $"/api/v1/groups/{groupId}/imports/analyze", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Equal("Only CSV files are allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task Analyze_EmptyFile_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([]);
        content.Add(fileContent, "file", "empty.csv");
        content.Add(new StringContent(((int)ImportType.SplitDuo).ToString()), "ImportTypeId");

        var response = await adminClient.PostAsync(
            $"/api/v1/groups/{groupId}/imports/analyze", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Equal("File is empty", body!.Error!.Message);
    }

    [Fact]
    public async Task Analyze_InvalidImportType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv, importTypeId: 99);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Equal("Invalid import type", body!.Error!.Message);
    }

    [Fact]
    public async Task Analyze_DuplicateFile_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        // First analyze succeeds
        var first = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second analyze of the same content → duplicate
        var second = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Equal("This file has already been imported", body!.Error!.Message);
    }

    #endregion

    #region Analyze — auth / errors

    [Fact]
    public async Task Analyze_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(SplitDuoCsv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "import.csv");
        content.Add(new StringContent(((int)ImportType.SplitDuo).ToString()), "ImportTypeId");

        var response = await Client.PostAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/imports/analyze", content, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme123");

        var response = await ImportTestHelpers.AnalyzeAsync(outsiderClient, groupId, SplitDuoCsv);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await ImportTestHelpers.AnalyzeAsync(client, Guid.NewGuid().ToString(), SplitDuoCsv);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetImports_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/imports", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetImports_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();
        var outsiderEmail = await TestDbSeeder.SeedUserAsync(Factory.Services, "outsider2@localhost");
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme123");

        var response = await outsiderClient.GetAsync(
            $"/api/v1/groups/{groupId}/imports", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Import (map + trigger) — happy path

    [Fact]
    public async Task Import_ValidMappings_Returns200_AndJobStarted()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Analyze first
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        // Map CSV members to user GUIDs and trigger
        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = user2Id,
            },
            CategoryMappings = new() { [(int)ExpenseCategory.Dining] = (int)ExpenseCategory.Dining },
            PaymentModeMappings = new() { [(int)PaymentMode.Cash] = (int)PaymentMode.Cash },
        };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Equal(importId, body.Data!.Id);
    }

    #endregion

    #region Import (map + trigger) — validation

    [Fact]
    public async Task Import_InvalidUserMapping_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, _) = await SetupGroupWithTwoMembersAsync();

        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = Guid.NewGuid().ToString(), // not a group member
            },
        };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Contains("maps to invalid or non-member user ID", body!.Error!.Message);
    }

    [Fact]
    public async Task Import_InvalidCategoryMapping_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = user2Id,
            },
            CategoryMappings = new() { [1] = 99 }, // 99 is not a valid category
        };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Contains("maps to invalid category", body!.Error!.Message);
    }

    [Fact]
    public async Task Import_InvalidPaymentModeMapping_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = user2Id,
            },
            PaymentModeMappings = new() { [1] = 99 }, // 99 is not a valid payment mode
        };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Contains("maps to invalid payment mode", body!.Error!.Message);
    }

    [Fact]
    public async Task Import_InvalidImportIdFormat_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var mapping = new ImportMappingDto { ImportId = "not-a-guid" };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        Assert.Equal("Invalid import ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task Import_NonexistentImport_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var mapping = new ImportMappingDto { ImportId = Guid.NewGuid().ToString() };

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{groupId}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Import_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var mapping = new ImportMappingDto { ImportId = Guid.NewGuid().ToString() };

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/groups/{Guid.NewGuid()}/imports", mapping, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region ProcessImportAsync — direct invocation (bypasses Quartz scheduler)

    [Fact]
    public async Task ProcessImportAsync_SplitDuoCsv_CreatesExpenses()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Analyze
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        // Map + trigger (saves mapping, schedules job — but scheduler thread is removed in test host)
        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = adminId,
                ["u2@localhost"] = user2Id,
            },
        };
        await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/imports", mapping, ct);

        // Directly invoke the job logic via DI (the Quartz hosted service is removed in tests)
        using var scope = Factory.Services.CreateScope();
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuo);

        // Verify expenses were created
        var listResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}/expenses", ct);
        var listBody = await listResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Equal(2, listBody!.Pagination.Total);

        // Verify import status updated to Completed
        var importsResponse = await adminClient.GetAsync($"/api/v1/groups/{groupId}/imports", ct);
        var importsBody = await importsResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ImportStatusDto>>(ct);
        var import = importsBody!.Data!.Single(i => i.Id == importId);
        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(2, import.RecordsCount);
        Assert.NotNull(import.CompletedAt);
    }

    [Fact]
    public async Task ProcessImportAsync_AliasModeGroup_ReturnsConflict()
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

        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, group.Id, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@splitduo.local"] = admin.Id,
                ["u2@localhost"] = user2.Id,
            },
        };
        await adminClient.PostAsJsonAsync($"/api/v1/groups/{group.Id}/imports", mapping, ct);

        using var scope = Factory.Services.CreateScope();
        await ImportTestHelpers.RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuo);

        // Import should fail because SplitDuo import type rejects alias-mode groups
        var importsResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/imports", ct);
        var importsBody = await importsResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ImportStatusDto>>(ct);
        var import = importsBody!.Data!.Single(i => i.Id == importId);
        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("alias mode", import.ErrorDetails);
    }

    #endregion
}
