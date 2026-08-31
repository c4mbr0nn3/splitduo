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

public class SplitDuoAliasImportsService(
    ILogger<SplitDuoAliasImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory,
    IImportValidatorService validatorService,
    TimeProvider timeProvider
) : AbstractImportService<SplitDuoAliasImportsService>(
    ImportType.SplitDuoAlias,
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
            var parseResult = await SplitDuoAliasCsvParser.ParseAsync(file);
            var fileHash = await HashUtils.ComputeSha256Async(file);

            var members = parseResult.Members
                .Select(m => new KeyValueDto { Key = m.Email, Value = m.Email })
                .ToList();

            var aliases = parseResult.Aliases
                .Select(a => new KeyValueDto { Key = a.Name, Value = a.Name })
                .ToList();

            var result = new ImportAnalysisDto
            {
                FileHash = fileHash,
                Members = members,
                Aliases = aliases,
                Categories = [],
                PaymentModes = []
            };

            Logger.LogInformation("Successfully analyzed SplitDuoAlias file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while analyzing SplitDuoAlias import file: {FileName}", file.FileName);
            return Result<ImportAnalysisDto>.InternalServerError($"Failed to analyze file: {e.Message}");
        }
    }

    public override async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
    {
        try
        {
            var parseResult = await SplitDuoAliasCsvParser.ParseAsync(file);
            var createResult = await CreateExpensesAsync(parseResult, groupId, importId);

            return createResult;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while processing SplitDuoAlias import file for ImportId {ImportId}",
                importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(SplitDuoAliasParseResult parseResult, int groupId, int importId)
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

            // Load group and validate alias mode
            var group = await UnitOfWork.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.DeletedAt == null);
            if (group == null)
            {
                return Result<int>.NotFound("Group not found");
            }

            if (!group.UseAliases)
            {
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Conflict("This group is not in alias mode");
            }

            if (!group.AliasSetupFinalized)
            {
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Conflict("Alias setup must be finalized before importing");
            }

            // Load existing aliases
            var existingAliases = await UnitOfWork.Aliases
                .Where(a => a.GroupId == group.Id && a.DeletedAt == null)
                .ToListAsync();

            var aliasByGuid = existingAliases.ToDictionary(a => a.Guid);

            // Load existing members with user and alias info
            var groupMembers = await UnitOfWork.GroupMembers
                .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
                .Include(gm => gm.User)
                .ToListAsync();

            var memberByEmail = groupMembers
                .Where(gm => gm.User != null)
                .ToDictionary(gm => gm.User.Email, gm => gm);

            var expensesToInsert = new List<Expense>();

            foreach (var exp in parseResult.Expenses)
            {
                // Parse category and payment mode from enum names
                if (!Enum.TryParse<ExpenseCategory>(exp.Category, true, out var category))
                {
                    Logger.LogWarning("Invalid category '{Category}' in expense '{Title}', defaulting to Other",
                        exp.Category, exp.Title);
                    category = ExpenseCategory.Other;
                }

                if (!Enum.TryParse<PaymentMode>(exp.PaymentMode, true, out var paymentMode))
                {
                    Logger.LogWarning("Invalid payment mode '{PaymentMode}' in expense '{Title}', defaulting to Other",
                        exp.PaymentMode, exp.Title);
                    paymentMode = PaymentMode.Other;
                }

                // Find payer using dynamic mapping - skip if not found
                if (!mappingConfig.UserMappings.TryGetValue(exp.PaidByEmail, out var payerIdStr) ||
                    !Guid.TryParse(payerIdStr, out var payerGuid))
                {
                    Logger.LogWarning(
                        "Payer '{PayerEmail}' not found in mapping configuration, skipping expense '{ExpenseTitle}'",
                        exp.PaidByEmail,
                        exp.Title);
                    continue;
                }

                var payerMembership = memberByEmail.Values
                    .FirstOrDefault(gm => gm.User.Guid == payerGuid);

                if (payerMembership == null)
                {
                    Logger.LogWarning(
                        "Payer user with GUID '{PayerGuid}' not found in group, skipping expense '{ExpenseTitle}'",
                        payerGuid, exp.Title);
                    continue;
                }

                // Build alias splits using dynamic mapping
                var aliasSplits = new List<ExpenseAliasSplit>();
                foreach (var entry in exp.ParsedAliasSplits)
                {
                    if (!mappingConfig.AliasMappings.TryGetValue(entry.AliasName, out var aliasIdStr) ||
                        !Guid.TryParse(aliasIdStr, out var aliasGuid))
                    {
                        Logger.LogWarning(
                            "Alias '{AliasName}' not found in mapping configuration for expense '{ExpenseTitle}'",
                            entry.AliasName, exp.Title);
                        continue;
                    }

                    if (!aliasByGuid.TryGetValue(aliasGuid, out var alias))
                    {
                        Logger.LogWarning(
                            "Alias with GUID '{AliasGuid}' not found in group for expense '{ExpenseTitle}'",
                            aliasGuid, exp.Title);
                        continue;
                    }

                    aliasSplits.Add(new ExpenseAliasSplit
                    {
                        AliasId = alias.Id,
                        SplitAmount = entry.Amount
                    });
                }

                if (aliasSplits.Count == 0)
                {
                    Logger.LogWarning("No valid alias splits found for expense '{ExpenseTitle}', skipping", exp.Title);
                    continue;
                }

                // Validate that splits sum up to total amount (allow for small rounding differences)
                var totalSplitAmount = aliasSplits.Sum(s => s.SplitAmount);
                var difference = Math.Abs(totalSplitAmount - exp.Amount);
                if (difference > 0.001m)
                {
                    Logger.LogWarning(
                        "Alias split amounts ({TotalSplitAmount:F2}) do not sum up to expense amount ({ExpenseAmount:F2}) for '{ExpenseTitle}', skipping",
                        totalSplitAmount, exp.Amount, exp.Title);
                    continue;
                }

                // Create expense with alias splits
                var expense = new Expense
                {
                    GroupId = groupId,
                    Title = exp.Title,
                    Description = exp.Description,
                    Amount = exp.Amount,
                    PaidBy = payerMembership.UserId,
                    ExpenseDate = exp.ParsedDate,
                    Category = category,
                    ExpenseTypeId = category == ExpenseCategory.Settlement ? (int)ExpenseType.Settlement : (int)ExpenseType.Normal,
                    PaymentMode = paymentMode,
                    ImportId = importId,
                    PaidByAliasId = payerMembership.AliasId
                };

                // Add alias splits using navigation property
                foreach (var aliasSplit in aliasSplits)
                {
                    expense.ExpenseAliasSplits.Add(aliasSplit);
                }

                expensesToInsert.Add(expense);
            }

            if (expensesToInsert.Count == 0)
            {
                Logger.LogWarning("No valid expenses to import");
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Success(0);
            }

            // Bulk insert expenses with their alias splits
            Logger.LogInformation("Bulk inserting {Count} expenses with their alias splits", expensesToInsert.Count);
            await UnitOfWork.Expenses.AddRangeAsync(expensesToInsert);
            await UnitOfWork.SaveChangesAsync();

            // Commit the transaction
            await UnitOfWork.CommitTransactionAsync();

            var totalSplits = expensesToInsert.Sum(e => e.ExpenseAliasSplits.Count);
            Logger.LogInformation("Successfully imported {ExpenseCount} expenses with {SplitCount} alias splits",
                expensesToInsert.Count, totalSplits);

            return Result<int>.Success(expensesToInsert.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error during SplitDuoAlias import processing, rolling back transaction for import {ImportId}",
                importId);
            await UnitOfWork.RollbackTransactionAsync();
            return Result<int>.InternalServerError($"Import failed: {ex.Message}");
        }
    }
}
