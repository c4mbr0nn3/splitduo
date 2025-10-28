using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class ResetPasswordRequestDto
{
    [Required] [EmailAddress] public string Email { get; set; } = "";

    [Required] public string Token { get; set; } = "";

    [Required, MinLength(8), PasswordComplexity]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "New password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = "";
}