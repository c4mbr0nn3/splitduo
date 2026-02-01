using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class LoginRequestDto
{
    [Required] [EmailAddress] public string Email { get; set; } = "";

    [Required] [MinLength(8)] public string Password { get; set; } = "";
}