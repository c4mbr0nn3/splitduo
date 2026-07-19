namespace SplitDuo.Core.Dto.Imports;

public class ImportMappingDto
{
    public string ImportId { get; set; } = "";
    public Dictionary<string, string> UserMappings { get; set; } = new();
    public Dictionary<int, int> CategoryMappings { get; set; } = new();
    public Dictionary<int, int> PaymentModeMappings { get; set; } = new();
}