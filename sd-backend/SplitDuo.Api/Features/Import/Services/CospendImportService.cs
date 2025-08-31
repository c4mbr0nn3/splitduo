using System.Globalization;
using CsvHelper;
using SplitDuo.Api.Features.Import.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Api.Features.Import.Services;

public class CospendImportService(ILogger<CospendImportService> logger, IUnitOfWork unitOfWork) : IImportService
{
    public async Task<Result<ImportStatusDto>> ImportFileAsync(IFormFile file, int groupId, int userId)
    {
        try
        {
            // Validate file type
            var validationResult = ValidateFile(file);
            if (validationResult.IsFailure)
            {
                return Result<ImportStatusDto>.BadRequest(validationResult.Error);
            }

            var import = new Core.Domain.Entities.Import
            {
                FileName = file.FileName,
                ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
                GroupId = groupId,
                UserId = userId
            };

            var streamReader = new StreamReader(file.OpenReadStream());
            var reader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
            
            // Configure CsvHelper to handle missing fields gracefully
            reader.Context.Configuration.MissingFieldFound = null;
            reader.Context.Configuration.HeaderValidated = null;
            var expenses = await ParseExpensesSection(reader);
            var result = await CreateExpensesAsync(expenses, groupId);
            if (result.IsFailure) throw new Exception(result.Error);

            import.RecordsCount = result.Value;
            import.Status = ImportStatus.Completed;
            await unitOfWork.Imports.AddAsync(import);
            await unitOfWork.SaveChangesAsync();

            var response = new ImportStatusDto(import);

            return Result<ImportStatusDto>.Success(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured while importing Cospend file");
            return Result<ImportStatusDto>.InternalServerError(e.Message);
        }
    }

    private async Task<List<CospendExpenseDto>> ParseExpensesSection(CsvReader reader)
    {
        var expenses = new List<CospendExpenseDto>();

        // Skip members section by reading until we find the expenses header
        var foundExpensesSection = false;

        while (await reader.ReadAsync())
        {
            // Check if current line is the expenses header
            var currentRecord = reader.Parser.Record;
            if (currentRecord is not { Length: > 0 }) continue;
            var firstField = currentRecord[0].Trim('"');
            if (firstField != "what") continue;
            foundExpensesSection = true;
            break;
        }

        if (!foundExpensesSection)
        {
            throw new InvalidOperationException("Expenses section not found in CSV file");
        }

        // Read the header to set up CsvHelper's mapping
        reader.ReadHeader();

        // Now read expenses until we hit an empty line (end of expenses section)
        while (await reader.ReadAsync())
        {
            var currentRecord = reader.Parser.Record;

            // Stop if we hit an empty line (section separator)
            if (IsEmptyOrSectionSeparator(currentRecord)) break;

            try
            {
                var expense = reader.GetRecord<CospendExpenseDto>();
                expenses.Add(expense);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to parse expense record at line {LineNumber}",
                    reader.Parser.RawRow);
            }
        }

        logger.LogInformation("Successfully parsed {Count} expense records", expenses.Count);
        return expenses;
    }

    // Data transformation methods
    private async Task<Result<int>> CreateExpensesAsync(List<CospendExpenseDto> expenses, int groupId)
    {
        var categoryMapping = BuildCategoryMapping();
        var paymentModeMapping = BuildPaymentModeMapping();
        var userNameMapping = BuildStaticUserMapping();

        var expensesToInsert = new List<Expense>();

        foreach (var exp in expenses.Where(e => !e.IsDeleted))
        {
            // Map category and payment mode
            var category = categoryMapping.GetValueOrDefault(exp.CategoryId, ExpenseCategory.Other);
            var paymentMode = paymentModeMapping.GetValueOrDefault(exp.PaymentModeId, PaymentMode.Other);

            // Find payer using static mapping - skip if not found
            if (!userNameMapping.TryGetValue(exp.PayerName, out var payerId)) continue;

            // Prepare owers list
            var owersUserIds = exp.OwersNames
                .Select(name => userNameMapping.GetValueOrDefault(name))
                .Where(id => id > 0)
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
                PaidBy = payerId,
                ExpenseDate = exp.ParsedDate,
                Category = category,
                PaymentMode = paymentMode
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
            return Result<int>.Success(0);
        }

        // Bulk insert expenses with their splits - single SaveChanges call
        logger.LogInformation("Bulk inserting {Count} expenses with their splits", expensesToInsert.Count);
        await unitOfWork.Expenses.AddRangeAsync(expensesToInsert);
        await unitOfWork.SaveChangesAsync();

        var totalSplits = expensesToInsert.Sum(e => e.ExpenseSplits.Count);
        logger.LogInformation("Successfully imported {ExpenseCount} expenses with {SplitCount} splits", 
            expensesToInsert.Count, totalSplits);

        return Result<int>.Success(expensesToInsert.Count);
    }

    private static Dictionary<int, PaymentMode> BuildPaymentModeMapping()
    {
        return new Dictionary<int, PaymentMode>
        {
            { 1, PaymentMode.Card },
            { 2, PaymentMode.Cash },
            { 3, PaymentMode.Other },
            { 4, PaymentMode.Transfer },
            { 5, PaymentMode.OnlineService }
        };
    }

    private static Dictionary<int, ExpenseCategory> BuildCategoryMapping()
    {
        return new Dictionary<int, ExpenseCategory>
        {
            { 5, ExpenseCategory.Groceries },
            { 15, ExpenseCategory.Groceries },
            { 9, ExpenseCategory.Groceries },
            { 6, ExpenseCategory.Dining },
            { 4, ExpenseCategory.Transportation },
            { 1, ExpenseCategory.Transportation },
            { 7, ExpenseCategory.Transportation },
            { 27, ExpenseCategory.Transportation },
            { 8, ExpenseCategory.Transportation },
            { 30, ExpenseCategory.Transportation },
            { 32, ExpenseCategory.Transportation },
            { 2, ExpenseCategory.Utilities },
            { 21, ExpenseCategory.Utilities },
            { 19, ExpenseCategory.Utilities },
            { 33, ExpenseCategory.Utilities },
            { 12, ExpenseCategory.Utilities },
            { 25, ExpenseCategory.Housing },
            { 16, ExpenseCategory.Housing },
            { 24, ExpenseCategory.Housing },
            { 35, ExpenseCategory.Housing },
            { 11, ExpenseCategory.Entertainment },
            { 28, ExpenseCategory.Entertainment },
            { 31, ExpenseCategory.Entertainment },
            { 13, ExpenseCategory.Entertainment },
            { 26, ExpenseCategory.Shopping },
            { 14, ExpenseCategory.Shopping },
            { 20, ExpenseCategory.Shopping },
            { 17, ExpenseCategory.Shopping },
            { 34, ExpenseCategory.Shopping },
            { 36, ExpenseCategory.Travel },
            { 22, ExpenseCategory.Health },
            { 18, ExpenseCategory.Education },
            { 23, ExpenseCategory.Other },
            { 3, ExpenseCategory.Other },
            { 10, ExpenseCategory.Other },
            { 29, ExpenseCategory.Other },
            { 0, ExpenseCategory.Other },
            { -11, ExpenseCategory.Other }
        };
    }

    private static Dictionary<string, int> BuildStaticUserMapping()
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Francesco", 1 },
            { "Beatrice", 2 }
        };
    }

    private ExpenseSplit[] CalculateEqualSplits(decimal amount, List<int> userIds)
    {
        if (userIds.Count == 0)
            throw new ArgumentException("User list cannot be empty", nameof(userIds));

        var splitAmount = Math.Round(amount / userIds.Count, 2, MidpointRounding.AwayFromZero);
        var splits = new List<ExpenseSplit>();
        var totalAssigned = 0m;

        // Assign equal amounts to all but the last user
        for (var i = 0; i < userIds.Count - 1; i++)
        {
            splits.Add(new ExpenseSplit
            {
                UserId = userIds[i],
                SplitAmount = splitAmount
            });
            totalAssigned += splitAmount;
        }

        // Assign the remainder to the last user to handle rounding differences
        var remainingAmount = amount - totalAssigned;
        splits.Add(new ExpenseSplit
        {
            UserId = userIds[userIds.Count - 1],
            SplitAmount = remainingAmount
        });

        return splits.ToArray();
    }

    private static Result ValidateFile(IFormFile file)
    {
        // Validate file is provided
        if (file == null)
        {
            return Result.BadRequest("No file provided");
        }

        // Validate file extension
        var fileName = file.FileName?.ToLowerInvariant();
        if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".csv"))
        {
            return Result.BadRequest("File must have a .csv extension");
        }

        // Validate file size (max 10MB as per specification)
        const long maxFileSizeBytes = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxFileSizeBytes)
        {
            return Result.BadRequest(
                $"File size must not exceed 10MB. Current size: {file.Length / 1024.0 / 1024.0:F2}MB");
        }

        // Validate file is not empty
        if (file.Length == 0)
        {
            return Result.BadRequest("File cannot be empty");
        }

        return Result.Success();
    }

    private static bool IsEmptyOrSectionSeparator(string[]? record)
    {
        return record == null
               || record.Length == 0
               || string.IsNullOrWhiteSpace(string.Join("", record));
    }
}