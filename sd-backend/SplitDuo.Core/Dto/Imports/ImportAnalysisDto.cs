using System.Text.Json.Serialization;

namespace SplitDuo.Core.Dto.Imports;

public class ImportAnalysisDto
{
    public string FileHash { get; set; } = "";
    [JsonPropertyName("members")] public List<KeyValueDto> Members { get; set; } = [];
    [JsonPropertyName("categories")] public List<KeyValueDto> Categories { get; set; } = [];
    [JsonPropertyName("paymentModes")] public List<KeyValueDto> PaymentModes { get; set; } = [];
    [JsonPropertyName("aliases")] public List<KeyValueDto> Aliases { get; set; } = [];
}