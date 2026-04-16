namespace SplitDuo.Core.Options;

public class AiOptions
{
    public const string SectionName = "Ai";
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public bool IsEnabled => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}