using SplitDuo.Core.Domain.Entities;

namespace SplitDuo.Api.Features.Users.Dto;

public class UserSettingsDto
{
    public string Theme { get; set; } = "auto";
    public string UiLanguage { get; set; } = "en";

    public UserSettingsDto() { }

    public UserSettingsDto(UserSettings settings)
    {
        Theme = settings.Theme;
        UiLanguage = settings.UiLanguage;
    }
}
