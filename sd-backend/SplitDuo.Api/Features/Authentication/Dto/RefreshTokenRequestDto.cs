using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class RefreshTokenRequestDto
{
    [Required] public string Token { get; set; } = "";
    [Required] public string RefreshToken { get; set; } = "";
}