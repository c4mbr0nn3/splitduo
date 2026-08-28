namespace SplitDuo.Api.Features.Users.Dto;

public class UserStatsDto
{
    public int TotalGroups { get; set; }
    public ModeBalanceDto Individual { get; set; } = new();
    public ModeBalanceDto Alias { get; set; } = new();
}