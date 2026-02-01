using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Settlements.Dto;

public class CreateSettlementRequestDto
{
    [Required] public string FromUserId { get; set; } = "";

    [Required] public string ToUserId { get; set; } = "";

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required] public string SettlementDate { get; set; } = "";

    public string? Description { get; set; }
}