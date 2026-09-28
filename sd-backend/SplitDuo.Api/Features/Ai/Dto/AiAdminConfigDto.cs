namespace SplitDuo.Api.Features.Ai.Dto;

/// <summary>Minimal live AI configuration for the admin page. Whitelisted fields only —
/// never the full AiOptions, and never the API key.</summary>
public class AiAdminConfigDto
{
    public bool Enabled { get; set; }
    public string? Model { get; set; }
    public string? BaseUrlHost { get; set; }
}