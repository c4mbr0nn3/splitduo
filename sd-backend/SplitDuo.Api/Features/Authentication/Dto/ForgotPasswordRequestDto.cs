using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class ForgotPasswordRequestDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
}