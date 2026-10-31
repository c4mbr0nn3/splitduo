using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Dto;

public class ChangeEmailRequestDto
{
    [Required, EmailAddress, MaxLength(255)]
    public string NewEmail { get; set; } = "";

    [Required]
    public string CurrentPassword { get; set; } = "";
}
