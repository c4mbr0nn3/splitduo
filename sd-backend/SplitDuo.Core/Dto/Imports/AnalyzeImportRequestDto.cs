using Microsoft.AspNetCore.Http;

namespace SplitDuo.Core.Dto.Imports;

public class AnalyzeImportRequestDto
{
    public IFormFile File { get; set; } = null!;
    public int GroupId { get; set; }
}