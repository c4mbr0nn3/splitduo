using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Dto;

public class ChangePasswordRequestDto
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required] [MinLength(6)] public string NewPassword { get; set; } = "";
}