using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // TODO: check this method
    [Authorize]
    [HttpPost("revoke")]
    public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
    {
        logger.LogInformation("Token revoke attempt");

        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return HandleResult(Result.Unauthorized("User not authenticated"));
        }

        // why user should revoke its own tokens? it should be an administrative endpoint i think
        var result = await authenticationService.RevokeTokenAsync(request.RefreshToken, userId.Value);

        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Token revoked successfully for user: {UserId}", userId);
        }
        else
        {
            logger.LogWarning("Token revoke failed for user: {UserId}. Error: {Error}", userId, result.Error);
        }

        return HandleResult(result, "Token revoked successfully");
    }
}