namespace SplitDuo.Core.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string? SecretKey { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required int Expires { get; set; }
}