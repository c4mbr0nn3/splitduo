using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.ImportExport.Dto;

public class ImportRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public string GroupId { get; set; } = "";
}