using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Authentication.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
    {
        // TODO: Implement login logic
        throw new NotImplementedException();
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Register([FromBody] RegisterRequestDto request)
    {
        // TODO: Implement registration logic
        throw new NotImplementedException();
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponseDto<AuthResponseDto>>> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        // TODO: Implement token refresh logic
        throw new NotImplementedException();
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponseDto<object>>> Logout()
    {
        // TODO: Implement logout logic
        throw new NotImplementedException();
    }
}