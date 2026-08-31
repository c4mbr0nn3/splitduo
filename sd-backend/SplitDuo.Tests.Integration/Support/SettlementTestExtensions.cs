using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Settlements.Dto;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for Settlements feature test setup.
/// </summary>
public static class SettlementTestExtensions
{
    /// <summary>
    /// Creates a settlement via POST /api/v1/groups/{groupId}/settlements and returns the SettlementDto.
    /// </summary>
    public static async Task<SettlementDto> CreateSettlementAsync(
        this HttpClient client,
        string groupId,
        object payload)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/settlements", payload, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<SettlementDto>>(ct);
        return body!.Data!;
    }
}