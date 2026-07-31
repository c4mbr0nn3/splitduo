using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Groups.Dto;

public class UpdateGroupMemberRoleRequestDto
{
    [Required] public string Role { get; set; } = "";
}
