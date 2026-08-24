using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[Route("api/v1/auth")]
public class AuthController(
    ILogger<AuthController> logger,
    IAuthenticationService authenticationService,
    IPasswordResetService passwordResetService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        logger.LogInformation("Login attempt for email: {Email}", request.Email);

        var result = await authenticationService.LoginAsync(request);

        if (result.IsFailure)
        {
            logger.LogWarning("Login failed for email: {Email}. Error: {Error}", request.Email, result.Error);
        }
        else
        {
            logger.LogInformation("Login successful for email: {Email}", request.Email);
            await unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, "Login successful");
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        logger.LogInformation("Token refresh attempt");

        var result = await authenticationService.RefreshTokenAsync(request);

        if (result.IsFailure)
        {
            logger.LogWarning("Token refresh failed. Error: {Error}", result.Error);
        }
        else
        {
            logger.LogInformation("Token refresh successful");
            await unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, "Token refreshed successfully");
    }

    [EnableRateLimiting("auth")]
    [HttpPost("verify-2fa")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> VerifyTwoFactor(
        [FromBody] VerifyTwoFactorLoginDto request)
    {
        logger.LogInformation("2FA verification attempt");

        var result = await authenticationService.VerifyTwoFactorAndCompleteLoginAsync(request);

        if (result.IsFailure)
        {
            logger.LogWarning("2FA verification failed. Error: {Error}", result.Error);
        }
        else
        {
            logger.LogInformation("2FA verification successful");
            await unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, "Two-factor authentication successful");
    }

    [Authorize]
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeMyToken([FromBody] RevokeTokenRequestDto request)
    {
        logger.LogInformation("Token revoke attempt");

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await authenticationService.RevokeRefreshTokenAsync(request.RefreshToken, currentUserId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Token revoked successfully for user: {UserId}", currentUserId);
        }
        else
        {
            logger.LogWarning("Token revoke failed for user: {UserId}. Error: {Error}", currentUserId, result.Error);
        }

        return HandleResult(result, "Token revoked successfully");
    }


    [Authorize(Policy = "SystemAdmin")]
    [HttpPost("{userGuid}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RevokeAllUserToken(string userGuid)
    {
        logger.LogInformation("Token revoke attempt for user {UserGuid}", userGuid);

        if (!Guid.TryParse(userGuid, out var userId))
            return HandleResult(Result.BadRequest("Invalid user guid"));

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == userId && u.DeletedAt == null);

        if (user == null)
            return HandleResult(Result.NotFound("User not found"));

        var result = await authenticationService.RevokeAllUserTokensAsync(user.Id, "All tokens revoked by system administrator");

        if (!result.IsSuccess)
        {
            logger.LogWarning("Token revoke failed for user: {UserGuid}. Error: {Error}", userGuid, result.Error);
            return HandleResult(result);
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Token revoked successfully for user: {UserGuid}", userGuid);

        return HandleResult(result, "Token revoked successfully");
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        logger.LogInformation("Password reset request for email: {Email}", request.Email);

        var result = await passwordResetService.InitiatePasswordResetAsync(request.Email);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Password reset email sent (if user exists) for email: {Email}", request.Email);
        }

        // Always return success to prevent email enumeration
        return HandleResult(Result.Success(),
            "If your email is registered, you will receive password reset instructions.");
    }

    [AllowAnonymous]
    [HttpGet("validate-reset-token")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponseDto<bool>>> ValidateResetToken([FromQuery] string email,
        [FromQuery] string token)
    {
        logger.LogInformation("Reset token validation attempt for email: {Email}", email);

        var result = await passwordResetService.ValidateResetTokenAsync(email, token);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Reset token validated successfully for email: {Email}", email);
        }
        else
        {
            logger.LogWarning("Reset token validation failed for email: {Email}. Error: {Error}", email, result.Error);
        }

        return HandleResult(result, "Reset token is valid");
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        logger.LogInformation("Password reset attempt for email: {Email}", request.Email);

        var result = await passwordResetService.ResetPasswordAsync(request);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Password reset successful for email: {Email}", request.Email);
        }
        else
        {
            logger.LogWarning("Password reset failed for email: {Email}. Error: {Error}", request.Email, result.Error);
        }

        return HandleResult(result, "Password reset successful. You can now log in with your new password.");
    }
}