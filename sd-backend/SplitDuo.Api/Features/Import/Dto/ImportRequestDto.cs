using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Import.Dto;

public class ImportRequestDto
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required] public string GroupId { get; set; } = "";
    [Required] public int ImportTypeId {get; set;}
}

public enum ImportType
{
    Cospend = 1
}