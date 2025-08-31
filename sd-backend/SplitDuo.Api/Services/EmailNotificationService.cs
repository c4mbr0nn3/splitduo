using System.Net;
using Microsoft.EntityFrameworkCore;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;

namespace SplitDuo.Api.Services;

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
    public async Task<Result<List<Notification>>> GetUnsentNotifications()
    {
        var notifications = await unitOfWork.Notifications.Where(x => !x.SentAt.HasValue).ToListAsync();
        return Result<List<Notification>>.Success(notifications);
    }

    public async Task<Result> SendAsync(Notification notification)
    {
        try
        {
            logger.LogInformation("Attempting to send email to {Email} with subject: {Subject}", 
                notification.To, notification.Subject);
            
            var result = await smtpService.SendEmailAsync(notification.To, notification.Subject, notification.Body);
            
            if (result.IsFailure)
            {
                logger.LogError("Failed to send email to {Email}: {Error}", 
                    notification.To, result.Error);
                return result;
            }
            
            notification.SentAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            logger.LogInformation("Successfully sent email to {Email}", notification.To);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while sending email to {Email}", notification.To);
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