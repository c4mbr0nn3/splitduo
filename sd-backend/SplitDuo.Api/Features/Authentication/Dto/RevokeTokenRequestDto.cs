namespace SplitDuo.Api.Features.Authentication.Dto;

public class RevokeTokenRequestDto
{
    public required string RefreshToken { get; set; }
}
