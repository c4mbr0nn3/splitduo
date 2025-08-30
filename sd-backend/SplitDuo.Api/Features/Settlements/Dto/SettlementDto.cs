using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Settlements.Dto;

public class SettlementDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string FromUserId { get; set; } = "";
    public UserBasicInfoDto FromUser { get; set; } = new();
    public string ToUserId { get; set; } = "";
    public UserBasicInfoDto ToUser { get; set; } = new();
    public decimal Amount { get; set; }
    public string SettlementDate { get; set; } = "";
    public string? Description { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}