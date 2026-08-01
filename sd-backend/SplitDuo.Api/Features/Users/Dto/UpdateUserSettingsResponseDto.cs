namespace SplitDuo.Api.Features.Users.Dto;

public class UpdateUserSettingsResponseDto
{
    public UserSettingsDto Settings { get; set; } = new();
    public string? Token { get; set; }
}
