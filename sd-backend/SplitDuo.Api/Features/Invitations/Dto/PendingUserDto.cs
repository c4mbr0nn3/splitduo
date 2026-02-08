namespace SplitDuo.Api.Features.Invitations.Dto;

public class PendingUserDto
{
    public string Email { get; set; } = "";
    public List<PendingUserGroupDto> Groups { get; set; } = [];
}

public class PendingUserGroupDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public long InvitedAt { get; set; }
    public long ExpiresAt { get; set; }
}