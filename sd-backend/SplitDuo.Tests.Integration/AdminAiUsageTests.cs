using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SplitDuo.Api.Features.Ai.Dto;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class AdminAiUsageTests(SplitDuoApiFactory factory) : IntegrationTest(factory)
{
    private async Task<HttpClient> LoginAsync(string email, string password = "changeme123")
    {
        var token = await GetAuthTokenAsync(email, password);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task<(HttpClient Admin, int AdminIntId)> AdminAsync()
    {
        var client = await CreateAuthenticatedClientAsync();
        var services = Factory.Services;
        var adminId = await TestDbSeeder.GetAdminIntIdAsync(services);
        return (client, adminId);
    }

    #region Config

    [Fact]
    public async Task GetConfig_Admin_ReturnsDisabledConfig()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, _) = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/admin/ai/config", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AiAdminConfigDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.False(body.Data.Enabled); // AI disabled in test host — endpoint must still work
        Assert.Null(body.Data.BaseUrlHost);
    }

    [Fact]
    public async Task GetConfig_NeverExposesApiKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, _) = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/admin/ai/config", ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Api_Key", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConfig_NonAdmin_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = Factory.Services;
        var email = await TestDbSeeder.SeedUserAsync(services, "nonadmin-config@localhost");
        var user = await LoginAsync(email);

        var response = await user.GetAsync("/api/v1/admin/ai/config", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region List

    [Fact]
    public async Task GetUsage_Admin_ReturnsSeededRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, success: true);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, success: false,
            inputTokens: null, outputTokens: null, totalTokens: null, latencyMs: null,
            errorMessage: "boom");

        var response = await admin.GetAsync("/api/v1/admin/ai/usage?page=1&limit=20", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body);
        Assert.Equal(2, body!.Data!.Count);
        Assert.Equal(2, body.Pagination.Total);
        Assert.All(body.Data, row => Assert.Equal("receipt_parse", row.Feature));
        Assert.All(body.Data, row => Assert.False(string.IsNullOrEmpty(row.UserId)));
        var failure = body.Data.Single(r => !r.Success);
        Assert.Equal("boom", failure.ErrorMessage);
    }

    [Fact]
    public async Task GetUsage_SuccessFilter_ReturnsOnlyFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, success: true);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, success: false,
            inputTokens: null, outputTokens: null, totalTokens: null, latencyMs: null,
            errorMessage: "boom");

        var response = await admin.GetAsync("/api/v1/admin/ai/usage?success=false", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Data!);
        Assert.All(body.Data, row => Assert.False(row.Success));
    }

    [Fact]
    public async Task GetUsage_LimitOver100_FallsBackToDefault20()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId);

        var response = await admin.GetAsync("/api/v1/admin/ai/usage?limit=500", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body);
        Assert.Equal(20, body!.Pagination.Limit);
        Assert.True(body.Data!.Count <= 20);
    }

    [Fact]
    public async Task GetUsage_NonAdmin_Returns403_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = Factory.Services;
        var email = await TestDbSeeder.SeedUserAsync(services, "nonadmin-usage@localhost");
        var user = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/ai/usage", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/admin/ai/usage", ct)).StatusCode);
    }

    [Fact]
    public async Task GetUsage_FromToFilter_ReturnsOnlyInWindowRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, requestedAt: 1_000_000);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, requestedAt: 2_000_000);

        // Window strictly inside the seeded epochs: pins both > / < boundary comparisons.
        var response = await admin.GetAsync("/api/v1/admin/ai/usage?from=999999&to=1500000", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body);
        Assert.Equal(1, body!.Pagination.Total);
        Assert.Equal(1_000_000, body.Data!.Single().RequestedAt);

        // Inclusive boundaries: from == to == row epoch must match exactly one row.
        var inclusive = await admin.GetAsync("/api/v1/admin/ai/usage?from=1000000&to=1000000", ct);
        Assert.Equal(HttpStatusCode.OK, inclusive.StatusCode);
        var inclusiveBody = await inclusive.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.Equal(1, inclusiveBody!.Pagination.Total);
        Assert.Equal(1_000_000, inclusiveBody.Data!.Single().RequestedAt);
    }

    [Fact]
    public async Task GetUsage_UserIdFilter_ReturnsOnlyThatUsersRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = Factory.Services;
        var (admin, _) = await AdminAsync();
        var emailA = await TestDbSeeder.SeedUserAsync(services, "ledger-a@localhost");
        var emailB = await TestDbSeeder.SeedUserAsync(services, "ledger-b@localhost");
        var intIdA = await TestDbSeeder.GetUserIntIdAsync(services, emailA);
        var intIdB = await TestDbSeeder.GetUserIntIdAsync(services, emailB);
        var guidA = await TestDbSeeder.GetUserGuidAsync(services, emailA);
        var guidB = await TestDbSeeder.GetUserGuidAsync(services, emailB);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, intIdA, requestedAt: 1_000_000);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, intIdB, requestedAt: 2_000_000);

        Assert.NotEqual(guidA, guidB);
        var response = await admin.GetAsync($"/api/v1/admin/ai/usage?userId={guidA}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body);
        Assert.Equal(1, body!.Pagination.Total);
        Assert.Equal(guidA.ToString(), body.Data!.Single().UserId);
    }

    [Fact]
    public async Task GetUsage_Pagination_Page2_Math()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, requestedAt: 1_000_000);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, requestedAt: 2_000_000);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId, requestedAt: 3_000_000);

        var page1 = await admin.GetAsync("/api/v1/admin/ai/usage?page=1&limit=2", ct);
        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        var body1 = await page1.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body1);
        Assert.Equal(3, body1!.Pagination.Total);
        Assert.Equal(2, body1.Pagination.TotalPages);
        Assert.True(body1.Pagination.HasNext);
        Assert.Equal(2, body1.Data!.Count);

        var page2 = await admin.GetAsync("/api/v1/admin/ai/usage?page=2&limit=2", ct);
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);
        var body2 = await page2.Content.ReadFromJsonAsync<PaginatedResponseDto<AiUsageEntryDto>>(ct);
        Assert.NotNull(body2);
        Assert.Equal(1, body2!.Data!.Count);
        Assert.False(body2.Pagination.HasNext);
        Assert.True(body2.Pagination.HasPrev);
    }

    [Fact]
    public async Task GetSummary_NonAdmin_Returns403_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = Factory.Services;
        var email = await TestDbSeeder.SeedUserAsync(services, "nonadmin-summary@localhost");
        var user = await LoginAsync(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/ai/usage/summary", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/admin/ai/usage/summary", ct)).StatusCode);
    }

    #endregion

    #region Summary

    [Fact]
    public async Task GetSummary_AggregatesSeededRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, adminId) = await AdminAsync();
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId,
            success: true, inputTokens: 10, outputTokens: 20, totalTokens: 30, latencyMs: 100);
        await TestDbSeeder.SeedAiCallLogAsync(Factory.Services, adminId,
            success: false, inputTokens: null, outputTokens: null, totalTokens: null, latencyMs: 5000,
            errorMessage: "boom");

        var response = await admin.GetAsync("/api/v1/admin/ai/usage/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AiUsageSummaryDto>>(ct);
        var s = body!.Data!;
        Assert.Equal(2, s.TotalCalls);
        Assert.Equal(1, s.SuccessfulCalls);
        Assert.Equal(1, s.FailedCalls);
        Assert.Equal(0.5, s.SuccessRate);
        Assert.Equal(10, s.TotalInputTokens);
        Assert.Equal(30, s.TotalTokens);
        Assert.NotNull(s.AvgLatencyMs);
        Assert.Single(s.ByDay);
        Assert.Single(s.ByModel);
        Assert.Single(s.ByUser);
    }

    [Fact]
    public async Task GetSummary_NoRows_ReturnsZeroedSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var (admin, _) = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/admin/ai/usage/summary", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AiUsageSummaryDto>>(ct);
        var s = body!.Data!;
        Assert.Equal(0, s.TotalCalls);
        Assert.Equal(0, s.SuccessfulCalls);
        Assert.Equal(0, s.FailedCalls);
        Assert.Equal(0, s.SuccessRate);
        Assert.Null(s.AvgLatencyMs);
        Assert.Empty(s.ByDay);
        Assert.Empty(s.ByModel);
        Assert.Empty(s.ByUser);
    }

    #endregion
}