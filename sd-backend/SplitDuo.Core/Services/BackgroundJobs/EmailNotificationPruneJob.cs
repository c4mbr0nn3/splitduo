using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.BackgroundJobs;

public class EmailNotificationPruneJob(
    ILogger<EmailNotificationPruneJob> logger,
    INotificationService notificationService,
    IUnitOfWork unitOfWork) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var result = await notificationService.GetPrunableNotifications();
            if (result.IsFailure) throw new Exception(result.Error);

            var notifications = result.Value;
            if (notifications == null || notifications.Count == 0) return;

            foreach (var notification in notifications)
            {
                var pruneResult = notificationService.Prune(notification);
                if (pruneResult.IsFailure) continue;
                await unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to prune email notifications");
            throw;
        }
    }
}