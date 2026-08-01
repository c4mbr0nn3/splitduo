using System.ComponentModel.DataAnnotations;
using SplitDuo.Core.Localization;

namespace SplitDuo.Api.Features.Users.Dto;

public class UpdateUserSettingsRequestDto
{
    [RegularExpression("^(light|dark|auto)$",
        ErrorMessage = "Theme must be 'light', 'dark', or 'auto'")]
    public string? Theme { get; set; }

    [SupportedLanguage]
    public string? UiLanguage { get; set; }
}
