using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class VerifyTwoFactorLoginDto
{
    [Required]
    public string Email { get; set; } = "";
    
    [Required]
    public string Code { get; set; } = "";
    
    public string CodeType { get; set; } = "totp"; // "totp", "email", "backup"
}