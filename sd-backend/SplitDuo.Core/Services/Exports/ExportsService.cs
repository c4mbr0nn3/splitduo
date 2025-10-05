using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SplitDuo.Core.Common;
using SplitDuo.Core.Persistence;

namespace SplitDuo.Core.Services.Exports;

public interface IExportsService
{
    Task<Result<byte[]>> ExportToCsvAsync(int groupId);
}

public class SplitDuoExportsService(
    IUnitOfWork unitOfWork,
    ILogger<SplitDuoExportsService> logger) : IExportsService
{
    public async Task<Result<byte[]>> ExportToCsvAsync(int groupId)
    {
        try
        {
            // Query all expenses for the group
            var expenses = await unitOfWork.Expenses
                .Where(e => e.GroupId == groupId && e.DeletedAt == null)
                .Include(e => e.PaidByUser)
                .Include(e => e.ExpenseSplits)
                .ThenInclude(s => s.User)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            if (expenses.Count == 0)
            {
                logger.LogInformation("No expenses found for group {GroupId}", groupId);
                return Result<byte[]>.Success([]);
            }

            // Generate CSV
            using var memoryStream = new MemoryStream();
            await using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write header
            csv.WriteField("Date");
            csv.WriteField("Title");
            csv.WriteField("Description");
            csv.WriteField("Amount");
            csv.WriteField("PaidByEmail");
            csv.WriteField("Category");
            csv.WriteField("PaymentMode");
            csv.WriteField("Owers");
            await csv.NextRecordAsync();

            // Write expense records
            foreach (var expense in expenses)
            {
                csv.WriteField(expense.ExpenseDate.ToString("yyyy-MM-dd"));
                csv.WriteField(expense.Title);
                csv.WriteField(expense.Description ?? string.Empty);
                csv.WriteField(expense.Amount);
                csv.WriteField(expense.PaidByUser?.Email ?? string.Empty);
                csv.WriteField(expense.Category.ToString());
                csv.WriteField(expense.PaymentMode.ToString());

                // Format owers as email:amount|email:amount
                var owersFormatted = string.Join("|",
                    expense.ExpenseSplits.Select(split => $"{split.User.Email}:{split.SplitAmount:F2}"));
                csv.WriteField(owersFormatted);

                await csv.NextRecordAsync();
            }

            await writer.FlushAsync();
            var csvBytes = memoryStream.ToArray();

            logger.LogInformation("Successfully exported {Count} expenses for group {GroupId}",
                expenses.Count, groupId);

            return Result<byte[]>.Success(csvBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting expenses for group {GroupId}", groupId);
            return Result<byte[]>.InternalServerError($"Failed to export expenses: {ex.Message}");
        }
    }
}