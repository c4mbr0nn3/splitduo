using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for Users feature test setup.
/// </summary>
public static class UserTestExtensions
{
    /// <summary>
    /// Calls GET /api/v1/users/me and returns the UserDto.
    /// </summary>
    public static async Task<UserDto> GetCurrentUserAsync(this HttpClient client)
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.GetAsync("/api/v1/users/me", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>(ct);
        return body!.Data!;
    }
}