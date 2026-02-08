using SplitDuo.Api.Features.Common.Dto;

namespace SplitDuo.Api.Features.Invitations.Dto;

public class InvitationDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public UserInfoDto InvitedBy { get; set; } = new();
    public string GroupName { get; set; } = "";
    public long InvitedAt { get; set; }
    public long ExpiresAt { get; set; }
}