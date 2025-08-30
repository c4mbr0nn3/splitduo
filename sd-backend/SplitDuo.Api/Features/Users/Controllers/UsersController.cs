using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;

namespace SplitDuo.Api.Features.Users.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponseDto<List<UserDto>>>> GetUsers()
    {
        // TODO: Implement get all users logic
        throw new NotImplementedException();
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> CreateUser([FromBody] CreateUserRequestDto request)
    {
        logger.LogInformation("Creating user with email: {Email}", request.Email);
        
        // TODO: Implement create user logic
        throw new NotImplementedException();
    }

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

    [HttpPut("me/password")]
    public async Task<ActionResult<ApiResponseDto<object>>> ChangeCurrentUserPassword(
        [FromBody] ChangePasswordRequestDto request)
    {
        logger.LogInformation("Password change attempt for current user");
        
        // TODO: Implement change current user password logic
        throw new NotImplementedException();
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> GetUser(string userId)
    {
        // TODO: Implement get user by ID logic
        throw new NotImplementedException();
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<ApiResponseDto<UserDto>>> UpdateUser(string userId,
        [FromBody] UpdateUserRequestDto request)
    {
        // TODO: Implement update user by ID logic
        throw new NotImplementedException();
    }

    [HttpDelete("{userId}")]
    public async Task<ActionResult<ApiResponseDto<object>>> DeleteUser(string userId)
    {
        // TODO: Implement delete user by ID logic
        throw new NotImplementedException();
    }
}