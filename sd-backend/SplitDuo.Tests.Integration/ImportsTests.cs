using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class ImportsTests : IntegrationTest
{
    public ImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    private static readonly string SplitDuoCsv = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@localhost,Dining,Cash,admin@localhost:15.00|u2@localhost:15.00
        2025-02-01,Bus,Trip,10.00,u2@localhost,Transportation,Card,u2@localhost:5.00|admin@localhost:5.00
        """;

    /// <summary>
    /// Uploads a CSV to POST /imports/analyze and returns the response.
    /// </summary>
    private static async Task<HttpResponseMessage> AnalyzeAsync(
        HttpClient client, string groupId, string csv, int importTypeId = (int)ImportType.SplitDuo,
        string fileName = "import.csv")
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new MultipartFormDataContent();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(importTypeId.ToString()), "ImportTypeId");

        return await client.PostAsync(
            $"/api/v1/groups/{groupId}/imports/analyze", content, ct);
    }

    /// <summary>
    /// Seeds a two-member group (admin + u2) and returns (adminClient, groupId, adminId, user2Id).
    /// </summary>
    private async Task<(HttpClient adminClient, string groupId, string adminId, string user2Id)>
        SetupGroupWithTwoMembersAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();
        var group = await adminClient.CreateGroupAsync();
        var admin = await adminClient.GetCurrentUserAsync();

        var memberEmail = await TestDbSeeder.SeedUserAsync(Factory.Services,
            "u2@localhost", "changeme", "Second", "User");
        await adminClient.PostAsJsonAsync(
            $"/api/v1/groups/{group.Id}/members", new { userEmail = memberEmail, role = "member" }, ct);
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var user2 = await memberClient.GetCurrentUserAsync();

        return (adminClient, group.Id, admin.Id, user2.Id);
    }

    /// <summary>
    /// Constructs an ImportProcessingJob with its DI dependencies. The job is not registered as
    /// a concrete service (Quartz instantiates it via the job type), so we build it manually.
    /// </summary>
    private static ImportProcessingJob CreateImportProcessingJob(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<ImportProcessingJob>>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var factory = services.GetRequiredService<IImportServiceFactory>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        return new ImportProcessingJob(logger, unitOfWork, factory, timeProvider);
    }

    /// <summary>
    /// Runs the import processing job directly (bypassing the Quartz scheduler thread, which is
    /// removed in the test host). Saves the mapping first via the HTTP endpoint, then invokes
    /// the job's Execute method with a synthetic IJobExecutionContext.
    /// </summary>
    private static async Task RunImportJobAsync(IServiceProvider services, string importId, ImportType importType)
    {
        var job = CreateImportProcessingJob(services);
        var scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var jobData = new JobDataMap
        {
            ["ImportGuid"] = importId,
            ["ImportType"] = importType.ToString(),
        };
        var jobDetail = JobBuilder.Create<ImportProcessingJob>()
            .WithIdentity($"test-import-{importId}")
            .UsingJobData(jobData)
            .Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"test-import-trigger-{importId}")
            .StartNow()
            .Build();
        await job.Execute(new TestJobExecutionContext(scheduler, jobDetail, trigger));
    }

    #endregion

    #region Analyze — happy path

    [Fact]
    public async Task Analyze_SplitDuoCsv_ReturnsAnalysisWithMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        var response = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

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
        Assert.Contains("admin@localhost", body.Data.AnalysisResults!);
        Assert.Contains("u2@localhost", body.Data.AnalysisResults);
    }

    [Fact]
    public async Task Analyze_CreatesImportRecord_InList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

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

        var response = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv, importTypeId: 99);

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
        var first = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second analyze of the same content → duplicate
        var second = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

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
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme");

        var response = await AnalyzeAsync(outsiderClient, groupId, SplitDuoCsv);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await AnalyzeAsync(client, Guid.NewGuid().ToString(), SplitDuoCsv);

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
        var outsiderClient = await CreateAuthenticatedClientAsync(outsiderEmail, "changeme");

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
        var analyzeResponse = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        // Map CSV members to user GUIDs and trigger
        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = adminId,
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

        var analyzeResponse = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = adminId,
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

        var analyzeResponse = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = adminId,
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

        var analyzeResponse = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = adminId,
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
        var analyzeResponse = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        // Map + trigger (saves mapping, schedules job — but scheduler thread is removed in test host)
        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = adminId,
                ["u2@localhost"] = user2Id,
            },
        };
        await adminClient.PostAsJsonAsync($"/api/v1/groups/{groupId}/imports", mapping, ct);

        // Directly invoke the job logic via DI (the Quartz hosted service is removed in tests)
        using var scope = Factory.Services.CreateScope();
        await RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuo);

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
        var memberClient = await CreateAuthenticatedClientAsync(memberEmail, "changeme");
        var user2 = await memberClient.GetCurrentUserAsync();

        var analyzeResponse = await AnalyzeAsync(adminClient, group.Id, SplitDuoCsv);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        var importId = analyzeBody!.Data!.Id;

        var mapping = new ImportMappingDto
        {
            ImportId = importId,
            UserMappings = new()
            {
                ["admin@localhost"] = admin.Id,
                ["u2@localhost"] = user2.Id,
            },
        };
        await adminClient.PostAsJsonAsync($"/api/v1/groups/{group.Id}/imports", mapping, ct);

        using var scope = Factory.Services.CreateScope();
        await RunImportJobAsync(scope.ServiceProvider, importId, ImportType.SplitDuo);

        // Import should fail because SplitDuo import type rejects alias-mode groups
        var importsResponse = await adminClient.GetAsync($"/api/v1/groups/{group.Id}/imports", ct);
        var importsBody = await importsResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ImportStatusDto>>(ct);
        var import = importsBody!.Data!.Single(i => i.Id == importId);
        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("alias mode", import.ErrorDetails);
    }

    #endregion
}

#region Test IJobExecutionContext

/// <summary>
/// Minimal IJobExecutionContext for directly invoking ImportProcessingJob.Execute in tests.
/// Only the JobDetail and JobDataMap are used by ImportProcessingJob; other members are stubs.
/// </summary>
file class TestJobExecutionContext(IScheduler scheduler, IJobDetail jobDetail, ITrigger trigger) : IJobExecutionContext
{
    public IScheduler Scheduler => scheduler;
    public IJobDetail JobDetail => jobDetail;
    public ITrigger Trigger => trigger;
    public ICalendar? Calendar => null;
    public bool Recovering => false;
    public TriggerKey RecoveringTriggerKey => new("test");
    public int RefireCount => 0;
    public JobDataMap JobDataMap => jobDetail.JobDataMap;
    public JobDataMap MergedJobDataMap => jobDetail.JobDataMap;
    public IJob JobInstance => null!;
    public CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    public DateTimeOffset FireTimeUtc => DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledFireTimeUtc => DateTimeOffset.UtcNow;
    public DateTimeOffset? PreviousFireTimeUtc => null;
    public DateTimeOffset? NextFireTimeUtc => null;
    public string FireInstanceId => "test-fire-instance";
    public object? Result { get; set; }
    public TimeSpan JobRunTime => TimeSpan.Zero;
    public void Put(object key, object value) { }
    public object? Get(object key) => null;
}

#endregion