using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class EmailNotificationProcessingJob(
    ILogger<EmailNotificationProcessingJob> logger,
    INotificationService notificationService,
    IUnitOfWork unitOfWork) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var result = await notificationService.GetUnsentNotifications();
            if (result.IsFailure) throw new Exception(result.Error);

            var notifications = result.Value;
            if (notifications == null || notifications.Count == 0) return;

            logger.LogInformation("Sending {Count} unsent notifications", notifications.Count);

            foreach (var notification in notifications)
            {
                var sendResult = await notificationService.SendAsync(notification);
                if (sendResult.IsFailure) continue;
                await unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process email notifications");
            throw;
        }
    }
}