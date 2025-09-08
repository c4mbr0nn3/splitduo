using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class VerifyTwoFactorSetupDto
{
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}