using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
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
        var importTypeString = context.JobDetail.JobDataMap.GetString("ImportType");

        // Validate required job data first
        if (string.IsNullOrEmpty(importGuid))
        {
            logger.LogError("ImportProcessingJob: Missing ImportGuid in job data");
            return;
        }

        if (!Guid.TryParse(importGuid, out var guid))
        {
            logger.LogError("ImportProcessingJob: Invalid ImportGuid format: {ImportGuid}", importGuid);
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

        if (string.IsNullOrEmpty(filePath))
        {
            await HandleImportFailure(import, "Missing file path in job data");
            return;
        }

        if (string.IsNullOrEmpty(importTypeString))
        {
            await HandleImportFailure(import, "Missing ImportType in job data");
            return;
        }

        if (!Enum.TryParse<ImportType>(importTypeString, out var importType))
        {
            await HandleImportFailure(import, $"Invalid ImportType: {importTypeString}");
            return;
        }

        await ProcessImport(import, filePath, importType);
    }

    private async Task HandleImportFailure(Import import, string errorMessage)
    {
        var importGuid = import.Guid.ToString();
        logger.LogError("ImportProcessingJob: {ErrorMessage} for Import: {ImportGuid}", errorMessage, importGuid);

        try
        {
            var currentTime = DateTimeOffset.UtcNow;
            import.Status = ImportStatus.Failed;
            import.CompletedAt = currentTime.ToUnixTimeSeconds();
            import.Duration = import.StartedAt.HasValue
                ? (currentTime.ToUnixTimeSeconds() - import.StartedAt.Value) * 1000
                : 0;
            import.ErrorDetails = errorMessage;

            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Updated import status to Failed for Import: {ImportGuid}", importGuid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update import status for Import: {ImportGuid}", importGuid);
        }
    }

    private async Task ProcessImport(Import import, string filePath, ImportType importType)
    {
        var startTime = DateTimeOffset.UtcNow;
        import.StartedAt = startTime.ToUnixTimeSeconds();
        import.Status = ImportStatus.Processing;

        try
        {
            await unitOfWork.SaveChangesAsync();

            // Get the appropriate import service using the factory
            var importService = importServiceFactory.GetImportService(importType);

            // Process import using the service - this now handles transactions internally
            var result = await importService.ProcessImportAsync(filePath, import.GroupId, import.Id);

            var completedTime = DateTimeOffset.UtcNow;
            import.CompletedAt = completedTime.ToUnixTimeSeconds();
            import.Duration = (completedTime - startTime).Milliseconds;

            if (result.IsSuccess)
            {
                import.Status = ImportStatus.Completed;
                import.RecordsCount = result.Value;
                logger.LogInformation(
                    "Import completed successfully: {ImportGuid}, Records: {RecordsCount}",
                    import.Guid,
                    result.Value);
            }
            else
            {
                import.Status = ImportStatus.Failed;
                import.ErrorDetails = result.Error;
                logger.LogError("Import failed: {ImportGuid}, Error: {Error}", import.Guid, result.Error);
            }
        }
        catch (Exception ex)
        {
            var completedTime = DateTimeOffset.UtcNow;
            import.CompletedAt = completedTime.ToUnixTimeSeconds();
            import.Duration = (completedTime - startTime).Milliseconds;
            import.Status = ImportStatus.Failed;
            import.ErrorDetails = ex.Message;

            logger.LogError(ex, "Import processing failed: {ImportGuid}", import.Guid);
        }
        finally
        {
            await unitOfWork.SaveChangesAsync();
            CleanupTempFile(import.Guid);
        }
    }

    private void CleanupTempFile(Guid importGuid)
    {
        try
        {
            FileUtils.CleanupTempFile(importGuid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete temporary file for Import: {ImportGuid}", importGuid);
        }
    }
}