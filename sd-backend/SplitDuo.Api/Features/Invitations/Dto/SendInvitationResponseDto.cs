using SplitDuo.Api.Features.Groups.Dto;

namespace SplitDuo.Api.Features.Invitations.Dto;

public class SendInvitationResponseDto
{
    public string Type { get; set; } = "";
    public GroupMemberDto? Member { get; set; }
    public InvitationDto? Invitation { get; set; }
}