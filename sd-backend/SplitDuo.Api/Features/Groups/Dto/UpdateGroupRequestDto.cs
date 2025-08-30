using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Groups.Dto;

public class UpdateGroupRequestDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Description { get; set; }
}