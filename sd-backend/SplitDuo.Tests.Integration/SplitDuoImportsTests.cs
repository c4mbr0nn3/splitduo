using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class SplitDuoImportsTests : IntegrationTest
{
    public SplitDuoImportsTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Helpers

    /// <summary>
    /// SplitDuo CSV with two expenses (30.00 paid by admin, 10.00 paid by u2).
    /// </summary>
    private static readonly string SplitDuoCsv = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,Dining,Cash,admin@splitduo.local:15.00|u2@localhost:15.00
        2025-02-01,Bus,Trip,10.00,u2@localhost,Transportation,Card,u2@localhost:5.00|admin@splitduo.local:5.00
        """;

    /// <summary>
    /// SplitDuo CSV with a single expense (30.00 paid by admin, split 15.00/15.00).
    /// </summary>
    private static readonly string SplitDuoCsvSingle = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,Dining,Cash,admin@splitduo.local:15.00|u2@localhost:15.00
        """;

    /// <summary>
    /// SplitDuo CSV whose category (InvalidCategory) is not a valid ExpenseCategory enum name.
    /// </summary>
    private static readonly string SplitDuoCsvInvalidCategory = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,InvalidCategory,Cash,admin@splitduo.local:15.00|u2@localhost:15.00
        """;

    /// <summary>
    /// SplitDuo CSV whose payment mode (InvalidMode) is not a valid PaymentMode enum name.
    /// </summary>
    private static readonly string SplitDuoCsvInvalidPaymentMode = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,Dining,InvalidMode,admin@splitduo.local:15.00|u2@localhost:15.00
        """;

    /// <summary>
    /// SplitDuo CSV whose payer (unknown@localhost) is not present in the user mappings.
    /// </summary>
    private static readonly string SplitDuoCsvUnknownPayer = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,unknown@localhost,Dining,Cash,admin@splitduo.local:15.00|u2@localhost:15.00
        """;

    /// <summary>
    /// SplitDuo CSV whose owers (unknown1/unknown2@localhost) are not in the user mappings.
    /// </summary>
    private static readonly string SplitDuoCsvUnknownOwers = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        2025-01-15,Lunch,Team lunch,30.00,admin@splitduo.local,Dining,Cash,unknown1@localhost:15.00|unknown2@localhost:15.00
        """;

    /// <summary>
    /// SplitDuo CSV with header only (no data rows).
    /// </summary>
    private static readonly string SplitDuoCsvEmpty = """
        Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
        """;

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

    /// <summary>
    /// Analyzes the CSV as a SplitDuo import and returns the import GUID.
    /// </summary>
    private async Task<string> AnalyzeAsync(HttpClient adminClient, string groupId, string csv)
    {
        var ct = TestContext.Current.CancellationToken;
        var analyzeResponse = await ImportTestHelpers.AnalyzeAsync(adminClient, groupId, csv);
        Assert.Equal(HttpStatusCode.OK, analyzeResponse.StatusCode);
        var analyzeBody = await analyzeResponse.Content.ReadFromJsonAsync<ApiResponseDto<ImportStatusDto>>(ct);
        return analyzeBody!.Data!.Id;
    }

    /// <summary>
    /// Analyzes the CSV as a SplitDuo import, then posts the mapping to trigger the import job.
    /// Returns the import GUID.
    /// </summary>
    private async Task<string> AnalyzeAndMapAsync(
        HttpClient adminClient, string groupId, string csv, ImportMappingDto mapping)
    {
        var ct = TestContext.Current.CancellationToken;
        var importId = await AnalyzeAsync(adminClient, groupId, csv);

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

    #region Process — error paths (via job flow)

    [Fact]
    public async Task Process_NoMappingConfiguration_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        // Analyze only — do NOT post a mapping, so MappingConfiguration stays null
        var importId = await AnalyzeAsync(adminClient, groupId, SplitDuoCsv);

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Failed, import.ImportStatusId);
        Assert.Contains("No mapping configuration", import.ErrorDetails);
    }

    [Fact]
    public async Task Process_InvalidCategory_DefaultsToOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvInvalidCategory,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal("Lunch", expense.Title);
        Assert.Equal((int)ExpenseCategory.Other, expense.CategoryId);
    }

    [Fact]
    public async Task Process_InvalidPaymentMode_DefaultsToOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvInvalidPaymentMode,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(1, import.RecordsCount);

        var expenses = await GetExpensesAsync(adminClient, groupId);
        var expense = Assert.Single(expenses);
        Assert.Equal("Lunch", expense.Title);
        Assert.Equal((int)PaymentMode.Other, expense.PaymentModeId);
    }

    [Fact]
    public async Task Process_PayerNotInMapping_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Payer email (unknown@localhost) is not in UserMappings → expense skipped
        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvUnknownPayer,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    [Fact]
    public async Task Process_NoValidSplits_SkipsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, _) = await SetupGroupWithTwoMembersAsync();

        // Map only the payer — ower emails are not in UserMappings → no valid splits
        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvUnknownOwers,
            CreateMapping(adminId));

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

        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvEmpty,
            CreateMapping(adminId, user2Id));

        var import = await RunJobAndGetStatusAsync(adminClient, groupId, importId);

        Assert.Equal((int)ImportStatus.Completed, import.ImportStatusId);
        Assert.Equal(0, import.RecordsCount);
        Assert.Empty(await GetExpensesAsync(adminClient, groupId));
    }

    #endregion

    #region Process — direct service invocation

    [Fact]
    public async Task Process_ImportNotFound_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, groupId, _, _) = await SetupGroupWithTwoMembersAsync();

        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var group = await unitOfWork.Groups.FirstAsync(g => g.Guid == Guid.Parse(groupId), ct);
        var service = scope.ServiceProvider
            .GetRequiredService<IImportServiceFactory>()
            .GetImportService(ImportType.SplitDuo);

        // 999999 is not a valid import Id. The job flow can't reach this path because
        // the job loads the import by GUID before invoking the service.
        var result = await service.ProcessImportAsync(new byte[0], group.Id, 999999);

        Assert.True(result.IsFailure);
        Assert.Contains("Import record not found", result.Error);
    }

    [Fact]
    public async Task Process_GroupNotFound_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (adminClient, groupId, adminId, user2Id) = await SetupGroupWithTwoMembersAsync();

        // Create an import with a mapping so the service passes the mapping check
        var importId = await AnalyzeAndMapAsync(adminClient, groupId, SplitDuoCsvSingle,
            CreateMapping(adminId, user2Id));

        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var import = await unitOfWork.Imports.FirstAsync(i => i.Guid == Guid.Parse(importId), ct);
        var service = scope.ServiceProvider
            .GetRequiredService<IImportServiceFactory>()
            .GetImportService(ImportType.SplitDuo);

        // 999999 is not a valid group Id. The job flow can't reach this path because
        // the job loads the import (with its group) before invoking the service.
        var result = await service.ProcessImportAsync(
            System.Text.Encoding.UTF8.GetBytes(SplitDuoCsvSingle), 999999, import.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Group not found", result.Error);
    }

    #endregion
}
