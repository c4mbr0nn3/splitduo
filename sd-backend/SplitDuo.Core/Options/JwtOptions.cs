namespace SplitDuo.Core.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string SecretKey { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int Expires { get; init; }
}