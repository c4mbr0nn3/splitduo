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
            var import = new Core.Domain.Entities.Import
            {
                FileName = file.FileName,
                ImportDate = DateOnly.FromDateTime(DateTime.UtcNow),
                GroupId = groupId,
                UserId = userId
            };

            var reader = new CsvReader(new StreamReader(file.OpenReadStream()), CultureInfo.InvariantCulture);
            var expenses = await ParseExpensesSection(reader);
            var result = await CreateExpensesAsync(expenses, groupId);
            if (result.IsFailure) throw new Exception(result.Error);

            import.RecordsCount = result.Value;
            import.Status = ImportStatus.Completed;
            await unitOfWork.Imports.AddAsync(import);

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
        throw new NotImplementedException();
    }

    // Data transformation methods
    private async Task<Result<int>> CreateExpensesAsync(List<CospendExpenseDto> expenses, int groupId)
    {
        var recordsCount = 0;
        var categoryMapping = BuildCategoryMapping();
        var paymentModeMapping = BuildPaymentModeMapping();
        var userNameMapping = BuildStaticUserMapping();

        foreach (var exp in expenses.Where(e => !e.IsDeleted))
        {
            // Map category
            var category = categoryMapping.GetValueOrDefault(exp.CategoryId, ExpenseCategory.Other);

            // Map payment mode
            var paymentMode = paymentModeMapping.GetValueOrDefault(exp.PaymentModeId, PaymentMode.Other);

            // Find payer using static mapping
            // Skip if payer not found in static mapping
            if (!userNameMapping.TryGetValue(exp.PayerName, out var payerId)) continue;

            // Create expense
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

            await unitOfWork.Expenses.AddAsync(expense);
            await unitOfWork.SaveChangesAsync(); // Need ID for splits

            // Create splits
            var owersUserIds = exp.OwersNames
                .Select(name => userNameMapping.GetValueOrDefault(name))
                .Where(id => id > 0)
                .ToList();

            if (owersUserIds.Count == 0) throw new Exception("No users were found for expense");

            var splits = CalculateEqualSplits(expense.Amount, owersUserIds);
            foreach (var split in splits)
            {
                split.ExpenseId = expense.Id;
            }

            await unitOfWork.ExpenseSplits.AddRangeAsync(splits);
            await unitOfWork.SaveChangesAsync();
            recordsCount++;
        }

        return Result<int>.Success(recordsCount);
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
        throw new NotImplementedException();
    }
}