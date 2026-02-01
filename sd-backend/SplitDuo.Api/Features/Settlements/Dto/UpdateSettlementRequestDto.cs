using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Settlements.Dto;

public class UpdateSettlementRequestDto
{
    [Range(0.01, double.MaxValue)] public decimal? Amount { get; set; }

    public string? SettlementDate { get; set; }

    public string? Description { get; set; }
}