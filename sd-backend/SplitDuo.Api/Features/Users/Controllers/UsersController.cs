using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Users.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(
    IUsersService usersService,
    IUnitOfWork unitOfWork,
    ILogger<UsersController> logger) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> GetUsers()
    {
        var result = await usersService.GetUsersAsync();
        return HandleResult(result, "Users retrieved successfully");
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<CreateUserResponseDto>>> CreateUser(
        [FromBody] CreateUserRequestDto request)
    {
        logger.LogInformation("Creating user with email: {Email}", request.Email);

        var result = await usersService.CreateUserAsync(request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User created successfully");
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<UserDto>.Unauthorized("User not authenticated"));

        var result = await usersService.GetUserAsync(currentUserId.Value.ToString());
        return HandleResult(result, "Current user retrieved successfully");
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateCurrentUser([FromBody] UpdateUserRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<UserDto>.Unauthorized("User not authenticated"));

        var result = await usersService.UpdateCurrentUserAsync(currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User profile updated successfully");
    }

    [HttpPut("me/password")]
    public async Task<ActionResult> ChangeCurrentUserPassword(
        [FromBody] ChangePasswordRequestDto request)
    {
        logger.LogInformation("Password change attempt for current user");

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));

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
    public async Task<ActionResult> DeleteUser(string userId)
    {
        var result = await usersService.DeleteUserAsync(userId);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User deleted successfully");
    }
}