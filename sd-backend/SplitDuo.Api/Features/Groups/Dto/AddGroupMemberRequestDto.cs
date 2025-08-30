using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Groups.Dto;

public class AddGroupMemberRequestDto
{
    [Required]
    [EmailAddress]
    public string UserEmail { get; set; } = "";

    public string Role { get; set; } = "member";
}