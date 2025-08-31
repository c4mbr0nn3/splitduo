using SplitDuo.Core.Common;

namespace SplitDuo.Api.Features.Users.Dto;

public class CreateUserDto
{
    public UserDto User { get; set; } = null!;
    public string GeneratedPassword { get; set; } = "";
}