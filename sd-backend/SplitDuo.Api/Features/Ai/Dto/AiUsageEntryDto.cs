namespace SplitDuo.Api.Features.Ai.Dto;

public class AiUsageEntryDto
{
    public string UserId { get; set; } = "";          // the USER's Guid (string) — row id is NOT exposed
    public string? UserDisplayName { get; set; }
    public string? UserEmail { get; set; }
    public string Feature { get; set; } = "";
    public string Model { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long RequestedAt { get; set; }             // Unix seconds
    public long? RespondedAt { get; set; }
    public int? LatencyMs { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
}