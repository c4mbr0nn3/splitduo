using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Imports.Parser;

namespace SplitDuo.Core.Services.Imports;

public class CospendImportsService(
    ILogger<CospendImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory,
    IImportValidatorService validatorService
) : AbstractImportService<CospendImportsService>(
    ImportType.Cospend,
    unitOfWork,
    validatorService,
    schedulerFactory,
    logger)
{
    public override async Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
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

            Logger.LogInformation("Successfully analyzed file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while analyzing import file: {FileName}", file.FileName);
            return Result<ImportAnalysisDto>.InternalServerError($"Failed to analyze file: {e.Message}");
        }
    }

    public override async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
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
            Logger.LogError(e, "An error occurred while processing import file for ImportId {ImportId}", importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(CospendParseResult parseResult, int groupId, int importId)
    {
        // Start a transaction to ensure all-or-nothing behavior
        await UnitOfWork.BeginTransactionAsync();

        try
        {
            // Get the import record to retrieve mapping configuration
            var import = await UnitOfWork.Imports.FirstOrDefaultAsync(i => i.Id == importId);
            if (import == null)
            {
                return Result<int>.NotFound("Import record not found");
            }

            // Get mapping configuration from the import record
            var mappingConfig = import.GetMappingConfiguration<ImportMappingDto>();
            if (mappingConfig == null)
            {
                Logger.LogError("No mapping configuration found for import {ImportId}", importId);
                return Result<int>.BadRequest("No mapping configuration found");
            }

            var groupMembers = await UnitOfWork.GroupMembers
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
                    Logger.LogWarning(
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
                    Logger.LogWarning("No valid users found for expense '{ExpenseTitle}', skipping", exp.What);
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
                Logger.LogWarning("No valid expenses to import");
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Success(0);
            }

            // Bulk insert expenses with their splits - single SaveChanges call
            Logger.LogInformation("Bulk inserting {Count} expenses with their splits", expensesToInsert.Count);
            await UnitOfWork.Expenses.AddRangeAsync(expensesToInsert);
            await UnitOfWork.SaveChangesAsync();

            // Commit the transaction
            await UnitOfWork.CommitTransactionAsync();

            var totalSplits = expensesToInsert.Sum(e => e.ExpenseSplits.Count);
            Logger.LogInformation("Successfully imported {ExpenseCount} expenses with {SplitCount} splits",
                expensesToInsert.Count, totalSplits);

            return Result<int>.Success(expensesToInsert.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during import processing, rolling back transaction for import {ImportId}",
                importId);
            await UnitOfWork.RollbackTransactionAsync();
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
}