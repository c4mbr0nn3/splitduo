using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Users.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetCurrentUser()
    {
        // TODO: Implement get current user logic
        throw new NotImplementedException();
    }

    [HttpPut("me")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateCurrentUser([FromBody] UpdateUserRequestDto request)
    {
        // TODO: Implement update current user logic
        throw new NotImplementedException();
    }

    [HttpDelete("me")]
    public async Task<ActionResult<ApiResponseDto<object>>> DeleteCurrentUser()
    {
        // TODO: Implement delete current user logic
        throw new NotImplementedException();
    }
}