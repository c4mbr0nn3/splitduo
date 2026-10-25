using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class ResetPasswordRequestDto
{
    // Required for the existing API contract and for the global rate limiter's
    // per-account partition. The reset token is the sole authority; the service
    // never compares this value to account state.
    [Required] [EmailAddress] public string Email { get; set; } = "";

    [Required] public string Token { get; set; } = "";

    [Required, MinLength(8), PasswordComplexity]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword), ErrorMessage = "New password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = "";
}