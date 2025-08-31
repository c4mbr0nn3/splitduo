using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Imports.Dto;

public class ImportRequestDto
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required] public int ImportTypeId { get; set; }
}