using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;
using SplitDuo.Core.Localization;

namespace SplitDuo.Api.Features.Invitations.Dto;

public class AcceptInvitationRequestDto
{
    [Required] public string Token { get; set; } = "";
    [Required] [MaxLength(100)] public string FirstName { get; set; } = "";
    [Required] [MaxLength(100)] public string LastName { get; set; } = "";
    [Required] [PasswordComplexity] public string Password { get; set; } = "";
    [Required] [Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";

    [SupportedLanguage]
    public string? UiLanguage { get; set; }
}