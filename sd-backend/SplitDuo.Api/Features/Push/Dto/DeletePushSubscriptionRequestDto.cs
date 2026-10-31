using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Push.Dto;

public class DeletePushSubscriptionRequestDto
{
    [Required] [MaxLength(500)] public string Endpoint { get; set; } = "";
}