using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;

namespace SplitDuo.Core.Services.Imports;

public abstract class AbstractImportService<T>(
    ImportType importType,
    IUnitOfWork unitOfWork,
    IImportValidatorService validatorService,
    ISchedulerFactory schedulerFactory,
    ILogger<T> logger) : IImportsService
{
    private ImportType ImportType { get; } = importType;
    protected readonly IUnitOfWork UnitOfWork = unitOfWork;
    protected readonly ILogger<T> Logger = logger;

    public abstract Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file);

    public async Task<Result<ImportStatusDto>> CreateImportJobAsync(
        IFormFile file,
        int groupId,
        int userId,
        ImportAnalysisDto analysisDto)
    {
        try
        {
            var byteFile = await FileUtils.ConvertToByteArrayAsync(file);
            var import = new Import
            {
                GroupId = groupId,
                UserId = userId,
                FileName = file.FileName,
                FileHash = analysisDto.FileHash,
                ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ImportType = ImportType,
                Status = ImportStatus.Pending,
                TempFile = byteFile
            };

            import.SetAnalysisResults(analysisDto);

            await UnitOfWork.Imports.AddAsync(import);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while creating import job");
            return Result<ImportStatusDto>.InternalServerError("Failed to create import job");
        }
    }

    public async Task<Result<ImportStatusDto>> UpdateImportMappingsAsync(
        Guid importGuid,
        ImportMappingDto mappingDto)
    {
        try
        {
            var import = await UnitOfWork.Imports.FirstOrDefaultAsync(i => i.Guid == importGuid);
            if (import == null)
            {
                return Result<ImportStatusDto>.NotFound("Import not found");
            }

            var validationResult = await validatorService.ValidateMappingConfigurationAsync(mappingDto, import.GroupId);
            if (validationResult.IsFailure)
            {
                return Result<ImportStatusDto>.BadRequest(validationResult.Error);
            }

            import.SetMappingConfiguration(mappingDto);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while updating import mappings for {ImportGuid}", importGuid);
            return Result<ImportStatusDto>.InternalServerError(e.Message);
        }
    }

    public async Task<Result<ImportStatusDto>> TriggerImportJobAsync(Guid importGuid)
    {
        try
        {
            var import = await UnitOfWork.Imports.FirstOrDefaultAsync(i => i.Guid == importGuid);
            if (import == null)
            {
                return Result<ImportStatusDto>.NotFound("Import not found");
            }

            var scheduler = await schedulerFactory.GetScheduler();
            var jobData = new JobDataMap
            {
                ["ImportGuid"] = import.Guid.ToString(),
                ["ImportType"] = import.ImportType.ToString()
            };

            var job = JobBuilder.Create<ImportProcessingJob>()
                .WithIdentity($"import-{import.Guid}")
                .UsingJobData(jobData)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"import-trigger-{import.Guid}")
                .StartNow()
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while triggering import job for {ImportGuid}", importGuid);
            return Result<ImportStatusDto>.InternalServerError(e.Message);
        }
    }

    public abstract Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId);
}