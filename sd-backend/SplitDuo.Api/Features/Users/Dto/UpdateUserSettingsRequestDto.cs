using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Dto;

public class UpdateUserSettingsRequestDto
{
    [RegularExpression("^(light|dark|auto)$",
        ErrorMessage = "Theme must be 'light', 'dark', or 'auto'")]
    public string? Theme { get; set; }

    [RegularExpression("^en$",
        ErrorMessage = "Unsupported language")]
    public string? UiLanguage { get; set; }
}
