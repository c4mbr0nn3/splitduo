using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Groups.Dto;

public class UpdateGroupRequestDto
{
    [MaxLength(200)] public string? Name { get; set; }

    public string? Description { get; set; }

    // UseAliases is intentionally absent — it is immutable after creation.
    // The DTO shape enforces this: JSON deserialization simply drops the field
    // if a client sends it, so no server-side rejection logic is needed.
}