using System.ComponentModel.DataAnnotations;

namespace SplitDuo.Api.Features.Invitations.Dto;

public class SendInvitationRequestDto
{
    [Required] [EmailAddress] public string Email { get; set; } = "";
}