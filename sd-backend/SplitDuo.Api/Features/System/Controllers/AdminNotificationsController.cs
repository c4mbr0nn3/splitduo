using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Api.Features.Common.Controllers;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.System.Dto;
using SplitDuo.Api.Features.System.Services;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.System.Controllers;

[ApiController]
[Route("api/v1/admin/notifications")]
[Authorize]
public class AdminNotificationsController(
    IEnumerable<INotificationProvider> notificationProviders,
    ICurrentVersionProvider currentVersionProvider,
    IUnitOfWork unitOfWork,
    ILogger<AdminNotificationsController> logger) : BaseApiController
{
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ApiResponseDto<List<AdminNotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponseDto<List<AdminNotificationDto>>>> GetNotifications(
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<List<AdminNotificationDto>>());

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null, ct);
        if (user == null)
        {
            logger.LogWarning("Admin notifications requested for unknown user {UserId}", currentUserId);
            return HandleResult(
                SplitDuo.Core.Common.Result<List<AdminNotificationDto>>.NotFound("User not found"));
        }

        var dismissed = user.Settings.DismissedNotifications;
        var notifications = new List<AdminNotificationDto>();

        foreach (var provider in notificationProviders)
        {
            IReadOnlyList<AdminNotification> pending;
            try
            {
                pending = await provider.GetPendingAsync(currentUserId.Value, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification provider {ProviderType} failed",
                    provider.GetType().Name);
                continue;
            }

            foreach (var notification in pending)
            {
                if (NotificationDismissalHelper.IsDismissed(dismissed, notification.Type, notification.TargetKey))
                    continue;

                notifications.Add(new AdminNotificationDto(notification.Type, notification.TargetKey, notification.Payload));
            }
        }

        return Ok(ApiResponseDto<List<AdminNotificationDto>>.SuccessResponse(
            notifications, "Notifications retrieved successfully"));
    }

    [HttpPost("dismiss")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponseDto<object>>> DismissNotification(
        [FromBody] DismissNotificationRequestDto request,
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return HandleResult(NotAuthenticated<object>());

        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.TargetKey))
            return BadRequest(ApiResponseDto<object>.ErrorResponse(
                "BAD_REQUEST", "Type and TargetKey are required"));

        if (request.Type.Length > NotificationDismissalHelper.MaxTypeLength ||
            request.TargetKey.Length > NotificationDismissalHelper.MaxTargetKeyLength)
            return BadRequest(ApiResponseDto<object>.ErrorResponse(
                "BAD_REQUEST", "Type must be at most 64 characters and TargetKey at most 128"));

        var user = await unitOfWork.Users
            .FirstOrDefaultAsync(u => u.Guid == currentUserId && u.DeletedAt == null, ct);
        if (user == null)
        {
            logger.LogWarning("Notification dismissal requested for unknown user {UserId}", currentUserId);
            return HandleResult(SplitDuo.Core.Common.Result<object>.NotFound("User not found"));
        }

        var pendingNotifications = new List<AdminNotification>();
        foreach (var provider in notificationProviders)
        {
            try
            {
                pendingNotifications.AddRange(
                    await provider.GetPendingAsync(currentUserId.Value, ct));
            }
            catch (Exception ex)
            {
                // Provider failure must not block dismissal of other providers' notifications
                logger.LogError(ex, "Notification provider {ProviderType} failed during dismissal",
                    provider.GetType().Name);
            }
        }

        if (!NotificationDismissalHelper.IsPendingNotification(pendingNotifications, request.Type, request.TargetKey))
        {
            logger.LogInformation(
                "Dismissal requested for non-pending notification {Type}/{TargetKey}",
                request.Type, request.TargetKey);
            return HandleResult(SplitDuo.Core.Common.Result<object>.NotFound(
                "Notification not found or no longer pending"));
        }

        // Pre-existing rows may deserialize the jsonb list as null despite the property initializer
        var dismissed = user.Settings.DismissedNotifications;
        if (dismissed == null)
        {
            dismissed = [];
            user.Settings.DismissedNotifications = dismissed;
        }

        dismissed.Add(new Core.Domain.Entities.DismissedNotification
        {
            Type = request.Type,
            TargetKey = request.TargetKey
        });

        // Prune stale entries + dedupe on write (pure logic, unit-testable)
        NotificationDismissalHelper.PruneAndDedupe(dismissed, currentVersionProvider.CurrentString);

        await unitOfWork.SaveChangesAsync(ct);

        return Ok(ApiResponseDto<object>.SuccessResponse(null!, "Notification dismissed"));
    }
}