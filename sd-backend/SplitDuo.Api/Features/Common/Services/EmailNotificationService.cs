using System.Net;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Features.Common.Services;

public interface INotificationService
{
    public Task<Result<List<Notification>>> GetUnsentNotifications();
    public Task<Result> SendAsync(Notification notification);
    public Task<Result> EnqueueAsync(Notification notification);
}

public class EmailNotificationService(
    ILogger<EmailNotificationService> logger,
    ISmtpService smtpService,
    IUnitOfWork unitOfWork) : INotificationService
{
    private const int MaxRetryCount = 3;

    public async Task<Result<List<Notification>>> GetUnsentNotifications()
    {
        var notifications = await unitOfWork.Notifications
            .Where(x => !x.SentAt.HasValue && x.RetryCount < MaxRetryCount)
            .ToListAsync();
        return Result<List<Notification>>.Success(notifications);
    }

    public async Task<Result> SendAsync(Notification notification)
    {
        try
        {
            logger.LogInformation(
                "Attempting to send email to {Email} with subject: {Subject} (Retry: {RetryCount}/{MaxRetry})",
                notification.To, notification.Subject, notification.RetryCount, MaxRetryCount);

            var result = await smtpService.SendEmailAsync(notification.To, notification.Subject, notification.Body);

            if (result.IsFailure)
            {
                notification.RetryCount++;
                notification.ErrorMessage = result.Error;

                logger.LogError("Failed to send email to {Email} (Retry {RetryCount}/{MaxRetry}): {Error}",
                    notification.To, notification.RetryCount, MaxRetryCount, result.Error);

                if (notification.RetryCount >= MaxRetryCount)
                {
                    logger.LogError("Max retry count reached for email to {Email}. Giving up.", notification.To);
                }

                return result;
            }

            notification.SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            notification.ErrorMessage = null;

            logger.LogInformation("Successfully sent email to {Email}", notification.To);
            return Result.Success();
        }
        catch (Exception ex)
        {
            notification.RetryCount++;
            notification.ErrorMessage = ex.Message;

            logger.LogError(ex, "Exception occurred while sending email to {Email} (Retry {RetryCount}/{MaxRetry})",
                notification.To, notification.RetryCount, MaxRetryCount);

            if (notification.RetryCount >= MaxRetryCount)
            {
                logger.LogError("Max retry count reached for email to {Email}. Giving up.", notification.To);
            }

            return Result.Failure("Failed to send email due to unexpected error", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result> EnqueueAsync(Notification notification)
    {
        try
        {
            logger.LogInformation("Enqueuing email notification to {Email} with subject: {Subject}",
                notification.To, notification.Subject);

            await unitOfWork.Notifications.AddAsync(notification);

            logger.LogDebug("Successfully enqueued email notification to {Email}", notification.To);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while enqueuing email notification to {Email}", notification.To);
            return Result.Failure("Failed to enqueue email notification", HttpStatusCode.InternalServerError);
        }
    }
}