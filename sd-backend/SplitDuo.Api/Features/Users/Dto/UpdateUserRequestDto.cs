using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Users.Dto;

public class UpdateUserRequestDto
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}