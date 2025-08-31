using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Import.Dto;

public class ImportRequestDto
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required] public int ImportTypeId { get; set; }
}