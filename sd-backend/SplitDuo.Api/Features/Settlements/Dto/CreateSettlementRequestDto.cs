using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Settlements.Dto;

public class CreateSettlementRequestDto
{
    [Required] public string FromUserId { get; set; } = "";
    public string? ToUserId { get; set; }
    public string? FromAliasId { get; set; }
    public string? ToAliasId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required] public string Date { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    public int? PaymentModeId { get; set; }
}