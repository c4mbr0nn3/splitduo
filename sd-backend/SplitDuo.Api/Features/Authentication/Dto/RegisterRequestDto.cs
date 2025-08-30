using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = "";

    [MaxLength(100)]
    public string? LastName { get; set; }
}