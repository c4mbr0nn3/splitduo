namespace SplitDuo.Core.Dto.Imports;

public class SplitDuoImportMappingDto
{
    public string ImportId { get; set; } = "";
    public Dictionary<string, string> UserMappings { get; set; } = new();
}
