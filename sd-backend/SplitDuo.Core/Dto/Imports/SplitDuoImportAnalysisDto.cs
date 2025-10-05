using System.Text.Json.Serialization;

namespace SplitDuo.Core.Dto.Imports;

public class SplitDuoImportAnalysisDto
{
    public string FileHash { get; set; } = "";
    [JsonPropertyName("users")] public List<KeyValueDto> Users { get; set; } = [];
    [JsonPropertyName("categories")] public List<KeyValueDto> Categories { get; set; } = [];
    [JsonPropertyName("paymentModes")] public List<KeyValueDto> PaymentModes { get; set; } = [];
}
