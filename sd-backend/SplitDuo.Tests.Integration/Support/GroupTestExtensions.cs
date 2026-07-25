using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Groups.Dto;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for Groups feature test setup.
/// </summary>
public static class GroupTestExtensions
{
    /// <summary>
    /// Creates a group via POST /api/v1/groups and returns the GroupDto.
    /// </summary>
    public static async Task<GroupDto> CreateGroupAsync(
        this HttpClient client,
        string name = "Test Group",
        string? description = null,
        bool useAliases = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync("/api/v1/groups", new
        {
            name,
            description,
            useAliases,
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<GroupDto>>(ct);
        return body!.Data!;
    }
}
