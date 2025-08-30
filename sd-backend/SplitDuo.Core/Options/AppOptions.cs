namespace SplitDuo.Core.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public required string Environment { get; set; }
    public required string BaseUrl { get; set; }
}