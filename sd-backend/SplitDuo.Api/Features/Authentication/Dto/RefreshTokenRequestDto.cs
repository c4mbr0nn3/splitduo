using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = "";
}