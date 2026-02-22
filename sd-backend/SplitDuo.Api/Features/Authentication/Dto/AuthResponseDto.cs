using SplitDuo.Api.Features.Users.Dto;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class AuthResponseDto
{
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public long ExpiresAt { get; set; }
    public bool RequiresTwoFactor { get; set; } = false;
    public string? TwoFactorChallengeToken { get; set; }
    public UserDto User { get; set; } = new();
}