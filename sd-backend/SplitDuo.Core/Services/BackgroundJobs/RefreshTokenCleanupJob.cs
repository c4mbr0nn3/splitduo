using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class RefreshTokenCleanupJob(
    ILogger<RefreshTokenCleanupJob> logger,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var cutoff = timeProvider.GetUtcNow().AddDays(-7).ToUnixTimeSeconds();
            var deletedCount = await unitOfWork.RefreshTokens
                .Where(rt => rt.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(context.CancellationToken);

            logger.LogInformation("Refresh token cleanup completed: {DeletedCount} expired tokens removed", deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup expired refresh tokens");
            throw;
        }
    }
}
