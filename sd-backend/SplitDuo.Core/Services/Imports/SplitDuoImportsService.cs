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

public class SplitDuoImportsService(
    ILogger<SplitDuoImportsService> logger,
    IUnitOfWork unitOfWork,
    ISchedulerFactory schedulerFactory,
    IImportValidatorService validatorService
) : AbstractImportService<SplitDuoImportsService>(
    ImportType.SplitDuo,
    unitOfWork,
    validatorService,
    schedulerFactory,
    logger)
{
    public override async Task<Result<ImportAnalysisDto>> AnalyzeFileAsync(IFormFile file)
    {
        try
        {
            var parseResult = await SplitDuoCsvParser.ParseAsync(file);
            var fileHash = await HashUtils.ComputeSha256Async(file);
            var members = parseResult.UniqueEmails
                .Select(email => new KeyValueDto { Key = email, Value = email })
                .ToList();
            var result = new ImportAnalysisDto
            {
                FileHash = fileHash,
                Members = members,
                Categories = [],
                PaymentModes = []
            };

            Logger.LogInformation("Successfully analyzed SplitDuo file with hash: {Hash}", fileHash);
            return Result<ImportAnalysisDto>.Success(result);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while analyzing SplitDuo import file: {FileName}", file.FileName);
            return Result<ImportAnalysisDto>.InternalServerError($"Failed to analyze file: {e.Message}");
        }
    }

    public override async Task<Result<int>> ProcessImportAsync(byte[] file, int groupId, int importId)
    {
        try
        {
            var parseResult = await SplitDuoCsvParser.ParseAsync(file);
            var createResult = await CreateExpensesAsync(parseResult, groupId, importId);

            return createResult;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "An error occurred while processing SplitDuo import file for ImportId {ImportId}",
                importId);
            return Result<int>.InternalServerError(e.Message);
        }
    }

    private async Task<Result<int>> CreateExpensesAsync(SplitDuoParseResult parseResult, int groupId, int importId)
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
            var mappingConfig = import.GetMappingConfiguration<SplitDuoImportMappingDto>();
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

                var payerUser = groupMembers.FirstOrDefault(u => u.Guid == payerGuid);
                if (payerUser == null)
                {
                    Logger.LogWarning(
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
                        Logger.LogWarning(
                            "User '{Email}' not found in mapping configuration for expense '{ExpenseTitle}'",
                            owner.Email, exp.Title);
                        continue;
                    }

                    var user = groupMembers.FirstOrDefault(u => u.Guid == userGuid);
                    if (user == null)
                    {
                        Logger.LogWarning("User with GUID '{UserGuid}' not found in group for expense '{ExpenseTitle}'",
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
                    Logger.LogWarning("No valid splits found for expense '{ExpenseTitle}', skipping", exp.Title);
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
                Logger.LogWarning("No valid expenses to import");
                await UnitOfWork.RollbackTransactionAsync();
                return Result<int>.Success(0);
            }

            // Bulk insert expenses with their splits
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
            Logger.LogError(ex,
                "Error during SplitDuo import processing, rolling back transaction for import {ImportId}",
                importId);
            await UnitOfWork.RollbackTransactionAsync();
            return Result<int>.InternalServerError($"Import failed: {ex.Message}");
        }
    }
}