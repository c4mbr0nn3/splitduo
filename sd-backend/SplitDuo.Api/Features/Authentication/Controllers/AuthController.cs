using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Authentication.Services;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[Route("api/v1/auth")]
public class AuthController(
    ILogger<AuthController> logger,
    IAuthenticationService authenticationService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpPost("login")]
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

    [HttpPost("verify-2fa")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> VerifyTwoFactor([FromBody] VerifyTwoFactorLoginDto request)
    {
        logger.LogInformation("2FA verification attempt for email: {Email}", request.Email);

        var result = await authenticationService.VerifyTwoFactorAndCompleteLoginAsync(request);

        if (result.IsFailure)
        {
            logger.LogWarning("2FA verification failed for email: {Email}. Error: {Error}", request.Email, result.Error);
        }
        else
        {
            logger.LogInformation("2FA verification successful for email: {Email}", request.Email);
            await unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, "Two-factor authentication successful");
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<ActionResult> RevokeMyToken([FromBody] RevokeTokenRequestDto request)
    {
        logger.LogInformation("Token revoke attempt");

        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(Result.Unauthorized("User not authenticated"));
        
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
    public async Task<ActionResult> RevokeAllUserToken(string userGuid)
    {
        logger.LogInformation("Token revoke attempt for user {UserGuid}", userGuid);

        var result = await authenticationService.RevokeAllUserTokensAsync(userGuid);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Token revoke failed for user: {UserGuid}. Error: {Error}", userGuid, result.Error);
            return HandleResult(result);
        }

        await unitOfWork.SaveChangesAsync();
        logger.LogInformation("Token revoked successfully for user: {UserGuid}", userGuid);

        return HandleResult(result, "Token revoked successfully");
    }
}

public class RevokeTokenRequestDto
{
    public required string RefreshToken { get; set; }
}