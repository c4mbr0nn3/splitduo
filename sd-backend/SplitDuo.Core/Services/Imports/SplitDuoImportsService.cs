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
using SplitDuo.Core.Services.Imports.Parser;

namespace SplitDuo.Core.Services.Imports;

public class SplitDuoImportsService(
    ILogger<SplitDuoImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory) : IImportsService
{
    public async Task<Result> IsValidImportAsync(IFormFile file, int groupId)
    {
        // Validate file extension
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Result.BadRequest("Invalid file format. Only CSV files are supported.");
        }

        // Validate file size (max 10MB)
        if (file.Length > 10 * 1024 * 1024)
        {
            return Result.BadRequest("File size exceeds 10MB limit.");
        }

        return Result.Success();
    }

    public async Task<Result> IsDuplicateFileAsync(string fileHash, int groupId)
    {
        var exists = await unitOfWork.Imports.AnyAsync(i =>
            i.FileHash == fileHash && i.GroupId == groupId);

        return exists
            ? Result.BadRequest("This file has already been imported.")
            : Result.Success();
    }

    public async Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
    {
        try
        {
            var parseResult = await SplitDuoCsvParser.ParseAsync(file);
            var fileHash = await HashUtils.ComputeSha256Async(file);
            var result = new ImportAnalysisDto
            {
                FileHash = fileHash,
                Members = parseResult.UniqueEmails
                    .Select(email => new KeyValueDto { Key = email, Value = email })
                    .ToList(),
                Categories = [],
                PaymentModes = []
            };

            logger.LogInformation("Successfully analyzed SplitDuo file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while analyzing SplitDuo import file: {FileName}", file.FileName);
            return Result<ImportAnalysisDto>.InternalServerError($"Failed to analyze file: {e.Message}");
        }
    }

    public async Task<Result<ImportStatusDto>> CreateImportJobAsync(
        IFormFile file,
        int groupId,
        int userId,
        ImportAnalysisDto analysisDto)
    {
        try
        {
            var duplicateCheck = await IsDuplicateFileAsync(analysisDto.FileHash, groupId);
            if (duplicateCheck.IsFailure)
            {
                return Result<ImportStatusDto>.BadRequest(duplicateCheck.Error);
            }

            // Read file into memory
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var import = new Import
            {
                GroupId = groupId,
                UserId = userId,
                FileName = file.FileName,
                FileHash = analysisDto.FileHash,
                ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = ImportStatus.Pending,
                ImportType = ImportType.SplitDuo,
                TempFile = fileBytes
            };

            import.SetAnalysisResults(analysisDto);

            await unitOfWork.Imports.AddAsync(import);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while creating SplitDuo import job");
            return Result<ImportStatusDto>.InternalServerError($"Failed to create import job: {e.Message}");
        }
    }

    public async Task<Result<ImportStatusDto>> UpdateImportMappingsAsync(
        Guid importGuid,
        CospendImportMappingDto mappingDto)
    {
        try
        {
            var import = await unitOfWork.Imports.FirstOrDefaultAsync(i => i.Guid == importGuid);
            if (import == null)
            {
                return Result<ImportStatusDto>.NotFound("Import not found");
            }

            // Convert CospendImportMappingDto to SplitDuoImportMappingDto structure
            var splitDuoMappingDto = new SplitDuoImportMappingDto
            {
                ImportId = importGuid.ToString(),
                UserMappings = mappingDto.UserMappings
            };

            // Validate mapping configuration
            var validationResult = await ValidateMappingConfiguration(splitDuoMappingDto, import.GroupId);
            if (validationResult.IsFailure)
            {
                return Result<ImportStatusDto>.BadRequest(validationResult.Error);
            }

            import.SetMappingConfiguration(splitDuoMappingDto);

            var statusDto = new ImportStatusDto
            {
                Id = import.Guid.ToString(),
                FileName = import.FileName,
                ImportDate = import.ImportDate.ToString("yyyy-MM-dd"),
                RecordsCount = import.RecordsCount,
                ImportStatusId = import.StatusId,
                ImportTypeId = (int)import.ImportType,
                MappingConfiguration = import.MappingConfiguration ?? string.Empty
            };

            return Result<ImportStatusDto>.Success(statusDto);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while updating SplitDuo import mappings for import {ImportGuid}",
                importGuid);
            return Result<ImportStatusDto>.InternalServerError($"Failed to update mappings: {e.Message}");
        }
    }

    public async Task<Result<ImportStatusDto>> TriggerImportJobAsync(Guid importGuid)
    {
        try
        {
            var import = await unitOfWork.Imports.FirstOrDefaultAsync(i => i.Guid == importGuid);
            if (import == null)
            {
                return Result<ImportStatusDto>.NotFound("Import not found");
            }

            if (import.Status != ImportStatus.Pending)
            {
                return Result<ImportStatusDto>.BadRequest(
                    $"Import is not in pending status (current: {import.Status})");
            }

            var mappingConfig = import.GetMappingConfiguration<SplitDuoImportMappingDto>();
            if (mappingConfig == null)
            {
                return Result<ImportStatusDto>.BadRequest("No mapping configuration found");
            }

            if (import.TempFile == null || import.TempFile.Length == 0)
            {
                return Result<ImportStatusDto>.BadRequest("No file data found");
            }

            // Schedule background job using Quartz
            var scheduler = await schedulerFactory.GetScheduler();
            var jobData = new JobDataMap
            {
                ["ImportGuid"] = import.Guid.ToString(),
                ["ImportType"] = nameof(ImportType.SplitDuo)
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

            logger.LogInformation("Scheduled SplitDuo import job for import {ImportGuid}", import.Guid);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while triggering SplitDuo import job for import {ImportGuid}",
                importGuid);
            return Result<ImportStatusDto>.InternalServerError($"Failed to trigger import: {e.Message}");
        }
    }

    public async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
    {
        try
        {
            var parseResult = await SplitDuoCsvParser.ParseAsync(file);
            var createResult = await CreateExpensesAsync(parseResult, groupId, importId);

            return createResult;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while processing SplitDuo import file for ImportId {ImportId}",
                importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(SplitDuoParseResult parseResult, int groupId, int importId)
    {
        // Start a transaction to ensure all-or-nothing behavior
        await unitOfWork.BeginTransactionAsync();

        try
        {
            // Get the import record to retrieve mapping configuration
            var import = await unitOfWork.Imports.FirstOrDefaultAsync(i => i.Id == importId);
            if (import == null)
            {
                return Result<int>.NotFound("Import record not found");
            }

            // Get mapping configuration from the import record
            var mappingConfig = import.GetMappingConfiguration<SplitDuoImportMappingDto>();
            if (mappingConfig == null)
            {
                logger.LogError("No mapping configuration found for import {ImportId}", importId);
                return Result<int>.BadRequest("No mapping configuration found");
            }

            var groupMembers = await unitOfWork.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .Select(gm => gm.User)
                .ToListAsync();

            var expensesToInsert = new List<Expense>();

            foreach (var exp in parseResult.Expenses)
            {
                // Parse category and payment mode from enum names
                if (!Enum.TryParse<ExpenseCategory>(exp.Category, true, out var category))
                {
                    logger.LogWarning("Invalid category '{Category}' in expense '{Title}', defaulting to Other",
                        exp.Category, exp.Title);
                    category = ExpenseCategory.Other;
                }

                if (!Enum.TryParse<PaymentMode>(exp.PaymentMode, true, out var paymentMode))
                {
                    logger.LogWarning("Invalid payment mode '{PaymentMode}' in expense '{Title}', defaulting to Other",
                        exp.PaymentMode, exp.Title);
                    paymentMode = PaymentMode.Other;
                }

                // Find payer using dynamic mapping - skip if not found
                if (!mappingConfig.UserMappings.TryGetValue(exp.PaidByEmail, out var payerIdStr) ||
                    !Guid.TryParse(payerIdStr, out var payerGuid))
                {
                    logger.LogWarning(
                        "Payer '{PayerEmail}' not found in mapping configuration, skipping expense '{ExpenseTitle}'",
                        exp.PaidByEmail,
                        exp.Title);
                    continue;
                }

                var payerUser = groupMembers.FirstOrDefault(u => u.Guid == payerGuid);
                if (payerUser == null)
                {
                    logger.LogWarning(
                        "Payer user with GUID '{PayerGuid}' not found in group, skipping expense '{ExpenseTitle}'",
                        payerGuid, exp.Title);
                    continue;
                }

                // Prepare splits using dynamic mapping
                var splits = new List<ExpenseSplit>();
                foreach (var owner in exp.ParsedOwers)
                {
                    if (!mappingConfig.UserMappings.TryGetValue(owner.Email, out var userIdStr) ||
                        !Guid.TryParse(userIdStr, out var userGuid))
                    {
                        logger.LogWarning(
                            "User '{Email}' not found in mapping configuration for expense '{ExpenseTitle}'",
                            owner.Email, exp.Title);
                        continue;
                    }

                    var user = groupMembers.FirstOrDefault(u => u.Guid == userGuid);
                    if (user == null)
                    {
                        logger.LogWarning("User with GUID '{UserGuid}' not found in group for expense '{ExpenseTitle}'",
                            userGuid, exp.Title);
                        continue;
                    }

                    splits.Add(new ExpenseSplit
                    {
                        UserId = user.Id,
                        SplitAmount = owner.Amount
                    });
                }

                if (splits.Count == 0)
                {
                    logger.LogWarning("No valid splits found for expense '{ExpenseTitle}', skipping", exp.Title);
                    continue;
                }

                // Create expense with splits
                var expense = new Expense
                {
                    GroupId = groupId,
                    Title = exp.Title,
                    Description = exp.Description,
                    Amount = exp.Amount,
                    PaidBy = payerUser.Id,
                    ExpenseDate = exp.ParsedDate,
                    Category = category,
                    PaymentMode = paymentMode,
                    ImportId = importId
                };

                // Add splits using navigation property
                foreach (var split in splits)
                {
                    expense.ExpenseSplits.Add(split);
                }

                expensesToInsert.Add(expense);
            }

            if (expensesToInsert.Count == 0)
            {
                logger.LogWarning("No valid expenses to import");
                await unitOfWork.RollbackTransactionAsync();
                return Result<int>.Success(0);
            }

            // Bulk insert expenses with their splits
            logger.LogInformation("Bulk inserting {Count} expenses with their splits", expensesToInsert.Count);
            await unitOfWork.Expenses.AddRangeAsync(expensesToInsert);
            await unitOfWork.SaveChangesAsync();

            // Commit the transaction
            await unitOfWork.CommitTransactionAsync();

            var totalSplits = expensesToInsert.Sum(e => e.ExpenseSplits.Count);
            logger.LogInformation("Successfully imported {ExpenseCount} expenses with {SplitCount} splits",
                expensesToInsert.Count, totalSplits);

            return Result<int>.Success(expensesToInsert.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error during SplitDuo import processing, rolling back transaction for import {ImportId}",
                importId);
            await unitOfWork.RollbackTransactionAsync();
            return Result<int>.InternalServerError($"Import failed: {ex.Message}");
        }
    }

    private async Task<Result> ValidateMappingConfiguration(SplitDuoImportMappingDto mappingDto, int groupId)
    {
        // Validate that all mapped user GUIDs exist in the group
        var groupMembers = await unitOfWork.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Include(gm => gm.User)
            .Select(gm => gm.User)
            .ToListAsync();

        var groupMemberGuids = groupMembers.Select(u => u.Guid.ToString()).ToHashSet();

        foreach (var mapping in mappingDto.UserMappings)
        {
            if (!groupMemberGuids.Contains(mapping.Value))
            {
                return Result.BadRequest($"User with GUID '{mapping.Value}' is not a member of this group");
            }
        }

        return Result.Success();
    }
}