using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Groups.Dto;

public class GroupMemberDto
{
    public string Id { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string UserId { get; set; } = "";
    public UserInfoDto User { get; set; } = new();
    public string Role { get; set; } = "";
    public long JoinedAt { get; set; }
}