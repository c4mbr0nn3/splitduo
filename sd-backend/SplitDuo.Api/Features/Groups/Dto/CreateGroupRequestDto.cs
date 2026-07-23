using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Groups.Dto;

public class CreateGroupRequestDto
{
    [Required] [MaxLength(200)] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool UseAliases { get; set; } = false;
}