using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;

namespace SplitDuo.Api.Features.Authentication.Dto;

public class RegisterRequestDto
{
    [Required] [EmailAddress] public string Email { get; set; } = "";

    [Required] [MinLength(8)] public string Password { get; set; } = "";

    [Required] [PersonName] [MaxLength(100)] public string FirstName { get; set; } = "";

    [PersonName] [MaxLength(100)] public string? LastName { get; set; }
}