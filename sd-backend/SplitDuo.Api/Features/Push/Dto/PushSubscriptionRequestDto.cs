using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Push.Dto;

public class PushSubscriptionRequestDto
{
    [Required] [MaxLength(500)] public string Endpoint { get; set; } = "";
    [Required] public PushSubscriptionKeysDto Keys { get; set; } = new();
    [MaxLength(500)] public string? UserAgent { get; set; }
}

public class PushSubscriptionKeysDto
{
    [Required] [MaxLength(255)] public string P256dh { get; set; } = "";
    [Required] [MaxLength(255)] public string Auth { get; set; } = "";
}