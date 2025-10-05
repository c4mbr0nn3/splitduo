using System.Text.Json.Serialization;
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

public class CospendImportsService(
    ILogger<CospendImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory) : IImportsService
{
    public async Task<Result> IsValidImportAsync(IFormFile file, int groupId)
    {
        var result = FileUtils.CheckExtensionAndSize(file);
        if (result.IsFailure) return result;

        var hash = await HashUtils.ComputeSha256Async(file);
        var isDuplicateResult = await IsDuplicateFileAsync(hash, groupId);

        return isDuplicateResult.IsSuccess
            ? Result.Conflict("This file has already been imported")
            : Result.Success();
    }

    public async Task<Result> IsDuplicateFileAsync(string fileHash, int groupId)
    {
        var any = await unitOfWork.Imports
            .AnyAsync(i => i.GroupId == groupId && i.FileHash == fileHash);
        return any ? Result.Success() : Result.NotFound("No duplicate file found");
    }

    public async Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
    {
        try
        {
            var sectionsToParse = new List<CospendSection>
            {
                CospendSection.Categories,
                CospendSection.Members,
                CospendSection.PaymentModes
            };
            var parseResult = await CospendCsvParser.ParseAsync(file, sectionsToParse);
            var fileHash = await HashUtils.ComputeSha256Async(file);
            var result = new ImportAnalysisDto
            {
                FileHash = fileHash,
                Members = parseResult.Members.Select(m => m.ToKeyValueDto()).ToList(),
                Categories = parseResult.Categories.Select(c => c.ToKeyValueDto()).ToList(),
                PaymentModes = parseResult.PaymentModes.Select(p => p.ToKeyValueDto()).ToList()
            };

            logger.LogInformation("Successfully analyzed file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while analyzing import file: {FileName}", file.FileName);
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
            var validationResult = FileUtils.CheckExtensionAndSize(file);
            if (validationResult.IsFailure)
            {
                return Result<ImportStatusDto>.BadRequest(validationResult.Error);
            }

            var byteFile = await FileUtils.ConvertToByteArrayAsync(file);
            var import = new Import
            {
                GroupId = groupId,
                UserId = userId,
                FileName = file.FileName,
                FileHash = analysisDto.FileHash,
                ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ImportType = ImportType.Cospend,
                Status = ImportStatus.Pending,
                TempFile = byteFile
            };

            import.SetAnalysisResults(analysisDto);

            await unitOfWork.Imports.AddAsync(import);

            var response = new ImportStatusDto(import);
            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while creating import job");
            return Result<ImportStatusDto>.InternalServerError("Failed to create import job");
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

            var validationResult = await ValidateMappingConfiguration(mappingDto, import.GroupId);
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
            logger.LogError(e, "An error occurred while updating import mappings for {ImportGuid}", importGuid);
            return Result<ImportStatusDto>.InternalServerError(e.Message);
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

            var scheduler = await schedulerFactory.GetScheduler();
            var jobData = new JobDataMap
            {
                ["ImportGuid"] = import.Guid.ToString(),
                ["ImportType"] = nameof(ImportType.Cospend)
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
            logger.LogError(e, "An error occurred while triggering import job for {ImportGuid}", importGuid);
            return Result<ImportStatusDto>.InternalServerError(e.Message);
        }
    }

    public async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
    {
        try
        {
            var sectionsToParse = new List<CospendSection>
            {
                CospendSection.Categories,
                CospendSection.Members,
                CospendSection.PaymentModes,
                CospendSection.Expenses
            };
            var parseResult = await CospendCsvParser.ParseAsync(file, sectionsToParse);
            var createResult = await CreateExpensesAsync(parseResult, groupId, importId);

            return createResult;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred while processing import file for ImportId {ImportId}", importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(CospendParseResult parseResult, int groupId, int importId)
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
            var mappingConfig = import.GetMappingConfiguration<CospendImportMappingDto>();
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

            foreach (var exp in parseResult.Expenses.Where(e => !e.IsDeleted))
            {
                // Map category and payment mode using dynamic mappings
                var category = mappingConfig.CategoryMappings
                    .TryGetValue(exp.CategoryId, out var categoryId)
                    ? (ExpenseCategory)categoryId
                    : ExpenseCategory.Other;

                var paymentMode = mappingConfig.PaymentModeMappings
                    .TryGetValue(exp.PaymentModeId, out var paymentModeId)
                    ? (PaymentMode)paymentModeId
                    : PaymentMode.Other;

                // Find payer using dynamic mapping - skip if not found
                if (!mappingConfig.UserMappings.TryGetValue(exp.PayerName, out var payerIdStr) ||
                    !Guid.TryParse(payerIdStr, out var payerGuid))
                {
                    logger.LogWarning(
                        "Payer '{PayerName}' not found in mapping configuration, skipping expense '{ExpenseTitle}'",
                        exp.PayerName,
                        exp.What);
                    continue;
                }

                // Prepare owers list using dynamic mapping
                var owersUserIds = exp.OwersNames
                    .Where(name => mappingConfig.UserMappings.TryGetValue(name, out var userIdStr)
                                   && Guid.TryParse(userIdStr, out _))
                    .Select(name => Guid.Parse(mappingConfig.UserMappings[name]))
                    .Select(guid => groupMembers.FirstOrDefault(u => u.Guid == guid)?.Id)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToList();

                if (owersUserIds.Count == 0)
                {
                    logger.LogWarning("No valid users found for expense '{ExpenseTitle}', skipping", exp.What);
                    continue;
                }


                // Create expense with splits using navigation property
                var expense = new Expense
                {
                    GroupId = groupId,
                    Title = exp.What,
                    Description = exp.Comment,
                    Amount = exp.Amount,
                    PaidBy = groupMembers.FirstOrDefault(u => u.Guid == payerGuid)?.Id ??
                             throw new InvalidOperationException("Payer user not found"),
                    ExpenseDate = exp.ParsedDate,
                    Category = category,
                    PaymentMode = paymentMode,
                    ImportId = importId
                };

                // Add splits using navigation property - EF Core will handle the relationships
                var splits = CalculateEqualSplits(expense.Amount, owersUserIds);
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

            // Bulk insert expenses with their splits - single SaveChanges call
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
            logger.LogError(ex, "Error during import processing, rolling back transaction for import {ImportId}",
                importId);
            await unitOfWork.RollbackTransactionAsync();
            return Result<int>.InternalServerError($"Import failed: {ex.Message}");
        }
    }

    private static List<ExpenseSplit> CalculateEqualSplits(decimal totalAmount, List<int> userIds)
    {
        var amountPerUser = Math.Round(totalAmount / userIds.Count, 2, MidpointRounding.AwayFromZero);

        var splits = userIds.Select(userId => new ExpenseSplit
        {
            UserId = userId,
            SplitAmount = amountPerUser
        }).ToList();

        // Handle rounding differences by adjusting the last split
        var totalSplitAmount = splits.Sum(s => s.SplitAmount);
        var difference = totalAmount - totalSplitAmount;

        if (difference != 0 && splits.Count > 0)
        {
            splits[^1].SplitAmount += difference;
        }

        return splits;
    }

    private async Task<Result> ValidateMappingConfiguration(CospendImportMappingDto mappings, int groupId)
    {
        var groupMemberIds = await unitOfWork.GroupMembers
            .Where(g => g.GroupId == groupId)
            .Include(g => g.User)
            .Select(g => g.User.Guid)
            .ToListAsync();

        foreach (var userMapping in mappings.UserMappings)
        {
            if (Guid.TryParse(userMapping.Value, out var userGuid) && groupMemberIds.Contains(userGuid)) continue;

            var message =
                $"User mapping for '{userMapping.Key}' maps to invalid or non-member user ID '{userMapping.Value}'";
            logger.LogWarning("User mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validCategories = Enum.GetValues<ExpenseCategory>().Cast<int>().ToHashSet();
        foreach (var categoryMapping in mappings.CategoryMappings)
        {
            if (validCategories.Contains(categoryMapping.Value)) continue;
            var message =
                $"Category mapping for ID '{categoryMapping.Key}' maps to invalid category '{categoryMapping.Value}'";
            logger.LogWarning("Category mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        var validPaymentModes = Enum.GetValues<PaymentMode>().Cast<int>().ToHashSet();
        foreach (var paymentModeMapping in mappings.PaymentModeMappings)
        {
            if (validPaymentModes.Contains(paymentModeMapping.Value)) continue;
            var message =
                $"Payment mode mapping for ID '{paymentModeMapping.Key}' maps to invalid payment mode '{paymentModeMapping.Value}'";
            logger.LogWarning("Payment mode mapping error: {Message}", message);
            return Result.BadRequest(message);
        }

        return Result.Success();
    }
}

public class ImportAnalysisDto
{
    public string FileHash { get; set; } = "";
    [JsonPropertyName("members")] public List<KeyValueDto> Members { get; set; } = [];
    [JsonPropertyName("categories")] public List<KeyValueDto> Categories { get; set; } = [];
    [JsonPropertyName("paymentModes")] public List<KeyValueDto> PaymentModes { get; set; } = [];
}