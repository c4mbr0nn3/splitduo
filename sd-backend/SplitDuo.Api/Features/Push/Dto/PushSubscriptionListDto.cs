namespace SplitDuo.Api.Features.Push.Dto;

public class PushSubscriptionListDto
{
    public string Id { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string? UserAgent { get; set; }
    public long CreatedAt { get; set; }
    public long? LastUsedAt { get; set; }
}