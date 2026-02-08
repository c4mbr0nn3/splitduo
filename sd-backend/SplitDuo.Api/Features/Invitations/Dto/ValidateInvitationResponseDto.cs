namespace SplitDuo.Api.Features.Invitations.Dto;

public class ValidateInvitationResponseDto
{
    public string Email { get; set; } = "";
    public string GroupName { get; set; } = "";
    public long ExpiresAt { get; set; }
}