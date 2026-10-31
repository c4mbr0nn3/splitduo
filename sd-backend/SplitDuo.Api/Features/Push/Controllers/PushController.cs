using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Common.Services;
using SplitDuo.Api.Features.Push.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Push.Controllers;

[ApiController]
[Route("api/v1/push")]
[Authorize]
public class PushController(
    IPushSubscriptionService pushSubscriptionService,
    IOptions<PushOptions> pushOptions,
    IUserContextService userContextService,
    IUnitOfWork unitOfWork,
    IStringLocalizer<PushController> loc) : BaseApiController
{
    private const string PushNotConfiguredCode = "PUSH_NOT_CONFIGURED";

    [HttpGet("vapid-public-key")]
    [ProducesResponseType(typeof(ApiResponseDto<VapidPublicKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<ApiResponseDto<VapidPublicKeyDto>> GetVapidPublicKey()
    {
        // A malformed key counts as unconfigured: the UI then explains the feature is
        // disabled instead of offering a toggle that can only fail.
        if (!pushOptions.Value.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponseDto<VapidPublicKeyDto>.ErrorResponse(
                PushNotConfiguredCode, loc["PushNotConfigured"]));

        return Ok(ApiResponseDto<VapidPublicKeyDto>.SuccessResponse(
            new VapidPublicKeyDto { PublicKey = pushOptions.Value.VapidPublicKey }));
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(ApiResponseDto<List<PushSubscriptionListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponseDto<List<PushSubscriptionListDto>>>> GetSubscriptions()
    {
        var user = await userContextService.GetCurrentUserAsync();
        if (user == null)
            return HandleResult(NotAuthenticated<List<PushSubscriptionListDto>>());

        var result = await pushSubscriptionService.GetByUserAsync(user.Id);
        if (result.IsFailure)
            return HandleResult(Result<List<PushSubscriptionListDto>>.InternalServerError(result.Error));

        var subscriptions = result.Value!.Select(MapToListDto).ToList();

        return Ok(ApiResponseDto<List<PushSubscriptionListDto>>.SuccessResponse(subscriptions));
    }

    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponseDto<object>>> UpsertSubscription(
        [FromBody] PushSubscriptionRequestDto request)
    {
        var user = await userContextService.GetCurrentUserAsync();
        if (user == null)
            return HandleResult(NotAuthenticated<object>());

        var userAgent = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(userAgent))
            userAgent = request.UserAgent;

        var result = await pushSubscriptionService.UpsertAsync(
            user.Id, request.Endpoint, request.Keys.P256dh, request.Keys.Auth, userAgent);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result);
    }

    [HttpDelete("subscriptions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> DeleteSubscription([FromBody] DeletePushSubscriptionRequestDto request)
    {
        var user = await userContextService.GetCurrentUserAsync();
        if (user == null)
            return HandleResult(NotAuthenticated());

        var result = await pushSubscriptionService.DeleteAsync(user.Id, request.Endpoint);

        if (result.IsSuccess)
            await unitOfWork.SaveChangesAsync();

        return HandleResult(result);
    }

    private static PushSubscriptionListDto MapToListDto(PushSubscription s) => new()
    {
        Id = s.Guid.ToString(),
        Endpoint = s.Endpoint,
        UserAgent = s.UserAgent,
        CreatedAt = s.CreatedAt,
        LastUsedAt = s.LastUsedAt,
    };
}