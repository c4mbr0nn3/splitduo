using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Invitations.Dto;
using SplitDuo.Api.Features.Invitations.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Users.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(
    IUsersService usersService,
    IInvitationsService invitationsService,
    IUnitOfWork unitOfWork,
    ILogger<UsersController> logger) : BaseApiController
{
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> GetUsers()
    {
        var result = await usersService.GetUsersAsync();
        return HandleResult(result, "Users retrieved successfully");
    }

    [HttpGet("pending")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<List<PendingUserDto>>>> GetPendingInvitations()
    {
        var result = await invitationsService.GetPendingInvitationsAsync();
        return HandleResult(result, "Pending invitations retrieved successfully");
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<UserDto>());

        var result = await usersService.GetUserAsync(currentUserId.Value.ToString());
        return HandleResult(result, "Current user retrieved successfully");
    }

    [HttpGet("me/stats")]
    public async Task<ActionResult<ApiResponseDto<UserStatsDto>>> GetUserStats()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<UserStatsDto>());

        var result = await usersService.GetCurrentUserStatsAsync(currentUserId.Value.ToString());
        return HandleResult(result, "Current user stats retrieved successfully");
    }

    [HttpGet("me/imports")]
    public async Task<ActionResult<ApiResponseDto<List<ImportStatusDto>>>> GetCurrentUserImports()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<ImportStatusDto>>());

        var result = await usersService.GetCurrentUserImports(currentUserId.Value.ToString());
        return HandleResult(result);
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateCurrentUser([FromBody] UpdateUserRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<UserDto>());

        var result = await usersService.UpdateCurrentUserAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User profile updated successfully");
    }

    [HttpPut("me/settings")]
    public async Task<ActionResult<ApiResponseDto<UpdateUserSettingsResponseDto>>> UpdateCurrentUserSettings(
        [FromBody] UpdateUserSettingsRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<UpdateUserSettingsResponseDto>());

        var result = await usersService.UpdateCurrentUserSettingsAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Settings updated successfully");
    }

    [HttpPut("me/password")]
    public async Task<ActionResult> ChangeCurrentUserPassword(
        [FromBody] ChangePasswordRequestDto request)
    {
        logger.LogInformation("Password change attempt for current user");

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await usersService.ChangeCurrentUserPasswordAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "Password changed successfully");
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetUser(string userId)
    {
        var result = await usersService.GetUserAsync(userId);
        return HandleResult(result, "User retrieved successfully");
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateUser(string userId,
        [FromBody] UpdateUserRequestDto request)
    {
        var result = await usersService.UpdateUserAsync(userId, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User updated successfully");
    }

    [HttpDelete("{userId}")]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        var result = await usersService.DeleteUserAsync(userId);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User deleted successfully");
    }
}