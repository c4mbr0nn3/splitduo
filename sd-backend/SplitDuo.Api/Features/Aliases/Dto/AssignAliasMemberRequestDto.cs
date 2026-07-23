using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Aliases.Dto;

public class AssignAliasMemberRequestDto
{
    [Required]
    public string UserId { get; set; } = "";
}
