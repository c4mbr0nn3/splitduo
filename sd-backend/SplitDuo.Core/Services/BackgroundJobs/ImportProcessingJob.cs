using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Factories;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.BackgroundJobs;

[DisallowConcurrentExecution]
public class ImportProcessingJob(
    ILogger<ImportProcessingJob> logger,
    IUnitOfWork unitOfWork,
    IImportServiceFactory importServiceFactory) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var importGuid = context.JobDetail.JobDataMap.GetString("ImportGuid");
        var filePath = context.JobDetail.JobDataMap.GetString("FilePath");

        if (string.IsNullOrEmpty(importGuid) || string.IsNullOrEmpty(filePath))
        {
            logger.LogError("ImportProcessingJob: Missing required job data (ImportGuid or FilePath)");
            return;
        }

        logger.LogInformation("Starting import processing for Import: {ImportGuid}", importGuid);

        var import = await unitOfWork.Imports
            .FirstOrDefaultAsync(i => i.Guid.ToString() == importGuid);

        if (import == null)
        {
            logger.LogError("Import not found: {ImportGuid}", importGuid);
            return;
        }

        var startTime = DateTimeOffset.UtcNow;
        import.StartedAt = startTime.ToUnixTimeSeconds();
        import.Status = ImportStatus.Processing;

        await unitOfWork.SaveChangesAsync();

        try
        {
            // Get the appropriate import service using the factory
            var importService = importServiceFactory.GetImportService(ImportType.Cospend); // TODO: Get from job data

            // Process import using the service
            var result = await importService.ProcessImportAsync(filePath, import.GroupId, import.UserId);

            var completedTime = DateTimeOffset.UtcNow;
            import.CompletedAt = completedTime.ToUnixTimeSeconds();
            import.DurationSeconds = (completedTime - startTime).Seconds;

            if (result.IsSuccess)
            {
                import.Status = ImportStatus.Completed;
                import.RecordsCount = result.Value;
                logger.LogInformation("Import completed successfully: {ImportGuid}, Records: {RecordsCount}",
                    importGuid, result.Value);
            }
            else
            {
                import.Status = ImportStatus.Failed;
                import.ErrorDetails = result.Error;
                logger.LogError("Import failed: {ImportGuid}, Error: {Error}", importGuid, result.Error);
            }
        }
        catch (Exception ex)
        {
            var completedTime = DateTimeOffset.UtcNow;
            import.CompletedAt = completedTime.ToUnixTimeSeconds();
            import.DurationSeconds = (completedTime - startTime).Seconds;
            import.Status = ImportStatus.Failed;
            import.ErrorDetails = ex.Message;

            logger.LogError(ex, "Import processing failed: {ImportGuid}", importGuid);
        }
        finally
        {
            await unitOfWork.SaveChangesAsync();

            // Clean up temporary file
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    logger.LogDebug("Deleted temporary file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete temporary file: {FilePath}", filePath);
            }
        }
    }
}