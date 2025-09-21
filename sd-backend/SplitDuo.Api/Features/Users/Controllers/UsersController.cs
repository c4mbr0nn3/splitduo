using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Api.Features.Users.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Users.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(
    IUsersService usersService,
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    ILogger<UsersController> logger) : BaseApiController
{
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> GetUsers()
    {
        var result = await usersService.GetUsersAsync();
        return HandleResult(result, "Users retrieved successfully");
    }

    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult<ApiResponseDto<CreateUserResponseDto>>> CreateUser(
        [FromBody] CreateUserRequestDto request)
    {
        logger.LogInformation("Creating user with email: {Email}", request.Email);

        var result = await usersService.CreateUserAsync(request);

        if (!result.IsSuccess) return HandleResult(result.MapTo<CreateUserResponseDto>());

        await unitOfWork.SaveChangesAsync();

        var data = result.Value!;

        var notification = new Notification
        {
            To = data.User.Email,
            Subject = "Welcome to SplitDuo - Your Account Has Been Created",
            Body = CreateWelcomeEmailBody(data.User, data.GeneratedPassword)
        };

        var emailResult = await notificationService.SendAsync(notification);

        if (emailResult.IsFailure)
            logger.LogWarning("Failed to send welcome email to {Email}: {Error}", request.Email, emailResult.Error);
        else
            logger.LogInformation("Welcome email sent successfully to {Email}", request.Email);

        var response = Result<CreateUserResponseDto>.Success(new CreateUserResponseDto { User = data.User });

        return HandleResult(response, "User created successfully");
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

    [HttpGet("me/stats")]
    public async Task<ActionResult<ApiResponseDto<UserStatsDto>>> GetUserStats()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<UserStatsDto>.Unauthorized("User not authenticated"));

        var result = await usersService.GetCurrentUserStatsAsync(currentUserId.Value.ToString());
        return HandleResult(result, "Current user stats retrieved successfully");
    }

    [HttpGet("me/imports")]
    public async Task<ActionResult<ApiResponseDto<List<ImportStatusDto>>>> GetCurrentUserImports()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result<List<ImportStatusDto>>.Unauthorized("User not authenticated"));

        var result = await usersService.GetCurrentUserImports(currentUserId.Value.ToString());
        return HandleResult(result);
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
    [Authorize(Policy = "SystemAdmin")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        var result = await usersService.DeleteUserAsync(userId);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result, "User deleted successfully");
    }

    private static string CreateWelcomeEmailBody(UserDto user, string generatedPassword)
    {
        return $"""
                <p>Hello {user.FullName},</p>
                <p>Your SplitDuo account has been successfully created. You can now start tracking and splitting expenses with your partner or group.</p>
                <p><strong>Your Login Credentials:</strong><br>
                Email: {user.Email}<br>
                Password: <strong>{generatedPassword}</strong></p>
                <p><strong>Important:</strong> Please change your password after your first login for security reasons.</p>
                <p>To get started:<br>
                1. Log in to SplitDuo using the credentials above<br>
                2. Change your password in your profile settings<br>
                3. Create or join a group to start splitting expenses</p>
                <p>If you didn't expect this email, please contact your administrator.</p>
                <p>Best regards,<br>
                The SplitDuo Team</p>
                """;
    }
}