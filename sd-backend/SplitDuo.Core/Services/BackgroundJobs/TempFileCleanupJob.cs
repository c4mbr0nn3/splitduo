using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class TempFileCleanupJob(ILogger<TempFileCleanupJob> logger, IUnitOfWork unitOfWork) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var completedFailedCutoff = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
            var counter = 0;
            var completedFailed = await unitOfWork.Imports
                .Where(i => (i.StatusId == (int)ImportStatus.Completed || i.StatusId == (int)ImportStatus.Failed)
                            && i.UpdatedAt < completedFailedCutoff
                            && i.TempFile != null).ToListAsync();

            foreach (var import in completedFailed)
            {
                import.TempFile = null;
            }

            await unitOfWork.SaveChangesAsync();
            counter += completedFailed.Count;

            var pendingProcessingCutoff = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds();
            var pendingProcessing = await unitOfWork.Imports
                .Where(i => (i.StatusId == (int)ImportStatus.Pending || i.StatusId == (int)ImportStatus.Processing)
                            && i.UpdatedAt < pendingProcessingCutoff
                            && i.TempFile != null).ToListAsync();
            foreach (var import in pendingProcessing)
            {
                import.TempFile = null;
            }

            await unitOfWork.SaveChangesAsync();
            counter += pendingProcessing.Count;

            logger.LogInformation("Temporary file cleanup completed: {DeletedFiles} files removed", counter);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to cleanup temporary files: {Message}", e.Message);
            throw;
        }
    }
}