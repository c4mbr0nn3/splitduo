using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Invitations.Dto;
using SplitDuo.Api.Features.Invitations.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Invitations.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class InvitationsController(
    IInvitationsService invitationsService,
    IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpPost("groups/{groupId}/invitations")]
    public async Task<ActionResult<ApiResponseDto<SendInvitationResponseDto>>> SendInvitation(
        string groupId, [FromBody] SendInvitationRequestDto request)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<SendInvitationResponseDto>());

        var result = await invitationsService.SendInvitationAsync(groupId, currentUserId.Value, request);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result);
    }

    [HttpGet("groups/{groupId}/invitations")]
    public async Task<ActionResult<ApiResponseDto<List<InvitationDto>>>> GetGroupInvitations(string groupId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<InvitationDto>>());

        var result = await invitationsService.GetGroupInvitationsAsync(groupId, currentUserId.Value);
        return HandleResult(result);
    }

    [HttpPost("groups/{groupId}/invitations/{invitationId}/resend")]
    public async Task<ActionResult<ApiResponseDto<InvitationDto>>> ResendInvitation(
        string groupId, string invitationId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<InvitationDto>());

        var result = await invitationsService.ResendInvitationAsync(groupId, invitationId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result);
    }

    [HttpDelete("groups/{groupId}/invitations/{invitationId}")]
    public async Task<ActionResult> RevokeInvitation(string groupId, string invitationId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated());

        var result = await invitationsService.RevokeInvitationAsync(groupId, invitationId, currentUserId.Value);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result);
    }

    [HttpGet("invitations/validate")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponseDto<ValidateInvitationResponseDto>>> ValidateToken(
        [FromQuery] string token)
    {
        var result = await invitationsService.ValidateInvitationTokenAsync(token);
        return HandleResult(result);
    }

    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    public async Task<ActionResult> AcceptInvitation([FromBody] AcceptInvitationRequestDto request)
    {
        // Service manages its own transaction (needs intermediate save to get User.Id)
        var result = await invitationsService.AcceptInvitationAsync(request);

        return HandleResult(result, "Account created successfully. You can now log in.");
    }
}