using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Aliases.Dto;

public class UpdateAliasRequestDto
{
    [MaxLength(100)]
    public string? Name { get; set; }
}
