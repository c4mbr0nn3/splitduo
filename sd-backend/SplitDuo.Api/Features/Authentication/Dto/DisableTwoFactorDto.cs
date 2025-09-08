using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class DisableTwoFactorDto
{
    [Required]
    public string Password { get; set; } = "";
}