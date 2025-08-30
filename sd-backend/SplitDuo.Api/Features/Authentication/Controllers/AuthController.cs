using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        logger.LogInformation("Login attempt for email: {Email}", request.Email);
        
        try
        {
            // TODO: Implement login logic
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed for email: {Email}", request.Email);
            throw;
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        logger.LogInformation("Token refresh attempt");
        
        try
        {
            // TODO: Implement token refresh logic
            throw new NotImplementedException();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Token refresh failed");
            throw;
        }
    }
}