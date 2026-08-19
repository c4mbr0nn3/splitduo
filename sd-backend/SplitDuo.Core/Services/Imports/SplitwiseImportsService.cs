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

public class SplitwiseImportsService(
    ILogger<SplitwiseImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory,
    IImportValidatorService validatorService,
    TimeProvider timeProvider
) : AbstractImportService<SplitwiseImportsService>(
    ImportType.Splitwise,
    unitOfWork,
    validatorService,
    schedulerFactory,
    logger,
    timeProvider)
{
    public override async Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
    {
        try
        {
            var parseResult = await SplitwiseCsvParser.ParseAsync(file);
            var fileHash = await HashUtils.ComputeSha256Async(file);

            var members = parseResult.Members
                .Select(name => new KeyValueDto { Key = name, Value = name })
                .ToList();

            var result = new ImportAnalysisDto
            {
                FileHash = fileHash,
                Members = members,
                Categories = parseResult.Categories,
                PaymentModes = []
            };

            Logger.LogInformation("Successfully analyzed Splitwise file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while analyzing Splitwise import file: {FileName}", file.FileName);
            return Result<ImportAnalysisDto>.InternalServerError($"Failed to analyze file: {e.Message}");
        }
    }

    public override async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
    {
        try
        {
            var parseResult = await SplitwiseCsvParser.ParseAsync(file);
            return await CreateExpensesAsync(parseResult, groupId, importId);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while processing Splitwise import file for ImportId {ImportId}",
                importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(SplitwiseParseResult parseResult, int groupId, int importId)
    {
        await UnitOfWork.BeginTransactionAsync();

        try
        {
            var import = await UnitOfWork.Imports.FirstOrDefaultAsync(i => i.Id == importId);
            if (import == null)
                return Result<int>.NotFound("Import record not found");

            var mappingConfig = import.GetMappingConfiguration<ImportMappingDto>();
            if (mappingConfig == null)
            {
                Logger.LogError("No mapping configuration found for import {ImportId}", importId);
                return Result<int>.BadRequest("No mapping configuration found");
            }

            // Guard: reject if group is in alias mode
            var group = await UnitOfWork.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.DeletedAt == null);
            if (group == null)
            {
                return Result<int>.NotFound("Group not found");
            }

            if (group.UseAliases)
            {
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Conflict("This group uses alias mode — use the SplitDuo Alias import type");
            }

            var groupMembers = await UnitOfWork.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .Select(gm => gm.User)
                .ToListAsync();

            // Build category name → ExpenseCategory mapping from user-provided mappings
            // CategoryMappings: Key = sequential int string (from parse), Value = ExpenseCategory enum name
            var expensesToInsert = new List<Expense>();

            // Build a lookup from category name to its sequential key
            var categoryKeyByName = parseResult.Categories.ToDictionary(c => c.Value, c => c.Key);

            foreach (var row in parseResult.Expenses)
            {
                // Resolve payer
                if (!mappingConfig.UserMappings.TryGetValue(row.PayerName, out var payerIdStr) ||
                    !Guid.TryParse(payerIdStr, out var payerGuid))
                {
                    Logger.LogWarning(
                        "Payer '{PayerName}' not found in mapping configuration, skipping expense '{Description}'",
                        row.PayerName, row.Description);
                    continue;
                }

                var payerUser = groupMembers.FirstOrDefault(u => u.Guid == payerGuid);
                if (payerUser == null)
                {
                    Logger.LogWarning(
                        "Payer user with GUID '{PayerGuid}' not found in group, skipping expense '{Description}'",
                        payerGuid, row.Description);
                    continue;
                }

                // Resolve category via mapping (Key = sequential int, Value = ExpenseCategory enum value)
                var category = ExpenseCategory.Other;
                if (categoryKeyByName.TryGetValue(row.Category, out var categoryKeyStr) &&
                    int.TryParse(categoryKeyStr, out var categoryKeyInt) &&
                    mappingConfig.CategoryMappings.TryGetValue(categoryKeyInt, out var categoryEnumValue))
                {
                    category = (ExpenseCategory)categoryEnumValue;
                }

                // Build splits
                var splits = new List<ExpenseSplit>();
                var totalOwed = 0m;

                foreach (var ower in row.Owers)
                {
                    if (!mappingConfig.UserMappings.TryGetValue(ower.Name, out var owerIdStr) ||
                        !Guid.TryParse(owerIdStr, out var owerGuid))
                    {
                        Logger.LogWarning(
                            "Ower '{Name}' not found in mapping configuration for expense '{Description}'",
                            ower.Name, row.Description);
                        continue;
                    }

                    var owerUser = groupMembers.FirstOrDefault(u => u.Guid == owerGuid);
                    if (owerUser == null)
                    {
                        Logger.LogWarning(
                            "Ower user with GUID '{OwnerGuid}' not found in group for expense '{Description}'",
                            owerGuid, row.Description);
                        continue;
                    }

                    splits.Add(new ExpenseSplit { UserId = owerUser.Id, SplitAmount = ower.Amount });
                    totalOwed += ower.Amount;
                }

                // Payer's own share = Cost − sum(ower amounts), include only if > 0
                var payerShare = row.Cost - totalOwed;
                if (payerShare > 0)
                {
                    splits.Add(new ExpenseSplit { UserId = payerUser.Id, SplitAmount = payerShare });
                }

                if (splits.Count == 0)
                {
                    Logger.LogWarning("No valid splits found for expense '{Description}', skipping", row.Description);
                    continue;
                }

                var expense = new Expense
                {
                    GroupId = groupId,
                    Title = row.Description,
                    Amount = row.Cost,
                    PaidBy = payerUser.Id,
                    ExpenseDate = row.Date,
                    Category = category,
                    PaymentMode = PaymentMode.Other,
                    ImportId = importId
                };

                foreach (var split in splits)
                    expense.ExpenseSplits.Add(split);

                expensesToInsert.Add(expense);
            }

            if (expensesToInsert.Count == 0)
            {
                Logger.LogWarning("No valid expenses to import");
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Success(0);
            }

            Logger.LogInformation("Bulk inserting {Count} expenses with their splits", expensesToInsert.Count);
            await UnitOfWork.Expenses.AddRangeAsync(expensesToInsert);
            await UnitOfWork.SaveChangesAsync();
            await UnitOfWork.CommitTransactionAsync();

            var totalSplits = expensesToInsert.Sum(e => e.ExpenseSplits.Count);
            Logger.LogInformation("Successfully imported {ExpenseCount} expenses with {SplitCount} splits",
                expensesToInsert.Count, totalSplits);

            return Result<int>.Success(expensesToInsert.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error during Splitwise import processing, rolling back transaction for import {ImportId}",
                importId);
            await UnitOfWork.RollbackTransactionAsync();
            return Result<int>.InternalServerError($"Import failed: {ex.Message}");
        }
    }
}