using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Services;
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

    [HttpPost("revoke")]
    public async Task<ActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
    {
        logger.LogInformation("Token revoke attempt");

        // Extract user ID from current JWT (this would require authentication)
        // For now, we'll extract from the expired token in the request
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = tokenHandler.ReadJwtToken(request.Token);
        var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "userId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return HandleResult(Core.Common.Result.Unauthorized("Invalid token"), null);
        }

        var result = await authenticationService.RevokeTokenAsync(request.RefreshToken, userId);

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