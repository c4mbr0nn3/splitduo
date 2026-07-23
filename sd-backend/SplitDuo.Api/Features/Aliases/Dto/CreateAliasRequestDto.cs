using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Aliases.Dto;

public class CreateAliasRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";
}
