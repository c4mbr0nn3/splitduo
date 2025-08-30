using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Services;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[Route("api/v1/auth")]
public class AuthController(
    ILogger<AuthController> logger,
    IAuthenticationService authenticationService) : BaseApiController
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
        }
        
        return HandleResult(result, "Token refreshed successfully");
    }
}