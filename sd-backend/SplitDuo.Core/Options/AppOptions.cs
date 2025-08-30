namespace SplitDuo.Core.Options;

public class AppOptions
{
    public const string SectionName = "App";

    public required string Environment { get; set; }
    public required string BaseUrl { get; set; }
    public required string InitialUserEmail { get; set; }
    public required string InitialUserFirstName { get; set; }
    public required string InitialUserLastName { get; set; }
    public required string InitialUserPassword { get; set; }
}