using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;

namespace SplitDuo.Api.Features.Users.Dto;

public class ChangePasswordRequestDto
{
    [Required] public string CurrentPassword { get; set; } = "";

    [Required, MinLength(8), PasswordComplexity]
    public string NewPassword { get; set; } = "";

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = "";
}