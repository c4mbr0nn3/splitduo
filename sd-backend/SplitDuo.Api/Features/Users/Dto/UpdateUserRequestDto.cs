using System.ComponentModel.DataAnnotations;
using SplitDuo.Api.Features.Users.Validation;
using SplitDuo.Core.Domain.Enums;

namespace SplitDuo.Api.Features.Users.Dto;

public class UpdateUserRequestDto
{
    [PersonName] [MaxLength(100)] public string? FirstName { get; set; }
    [PersonName] [MaxLength(100)] public string? LastName { get; set; }

    // Kept only so the API can reject an attempted email change explicitly.
    // Removing the property would make System.Text.Json ignore it silently.
    [EmailAddress] public string? Email { get; set; }

    public GlobalRole? GlobalRole { get; set; }
}