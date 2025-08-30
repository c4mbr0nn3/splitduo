using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Dto;

public class CreateUserRequestDto
{
    [Required] public string Email { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    [Required] [MinLength(6)] public string Password { get; set; } = "";
}