using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
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
            // Load the group to check alias mode
            var group = await unitOfWork.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId && g.DeletedAt == null);

            if (group == null)
            {
                return Result<byte[]>.NotFound("Group not found");
            }

            if (group.UseAliases)
            {
                return await ExportAliasCsvAsync(group);
            }

            // Query all expenses for the group (per-user mode)
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

    private async Task<Result<byte[]>> ExportAliasCsvAsync(Group group)
    {
        try
        {
            // Load aliases
            var aliases = await unitOfWork.Aliases
                .Where(a => a.GroupId == group.Id && a.DeletedAt == null)
                .ToListAsync();

            // Load members with user and alias
            var members = await unitOfWork.GroupMembers
                .Where(gm => gm.GroupId == group.Id && gm.DeletedAt == null)
                .Include(gm => gm.User)
                .Include(gm => gm.Alias)
                .ToListAsync();

            // Load expenses with alias splits
            var expenses = await unitOfWork.Expenses
                .Where(e => e.GroupId == group.Id && e.DeletedAt == null)
                .Include(e => e.PaidByUser)
                .Include(e => e.PaidByAlias)
                .Include(e => e.ExpenseAliasSplits)
                .ThenInclude(eas => eas.Alias)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();

            if (expenses.Count == 0 && aliases.Count == 0)
            {
                logger.LogInformation("No expenses or aliases found for group {GroupId}", group.Id);
                return Result<byte[]>.Success([]);
            }

            using var memoryStream = new MemoryStream();
            await using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
            await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Section 1: aliases
            csv.WriteField("name");
            csv.WriteField("is_singleton");
            await csv.NextRecordAsync();

            foreach (var alias in aliases)
            {
                csv.WriteField(alias.Name);
                csv.WriteField(alias.IsSingleton == true ? 1 : 0);
                await csv.NextRecordAsync();
            }

            // Blank line separator
            await csv.NextRecordAsync();

            // Section 2: members
            csv.WriteField("email");
            csv.WriteField("alias_name");
            csv.WriteField("role");
            await csv.NextRecordAsync();

            foreach (var member in members)
            {
                csv.WriteField(member.User?.Email ?? string.Empty);
                csv.WriteField(member.Alias?.Name ?? string.Empty);
                csv.WriteField(member.Role.ToString().ToLowerInvariant());
                await csv.NextRecordAsync();
            }

            // Blank line separator
            await csv.NextRecordAsync();

            // Section 3: expenses
            csv.WriteField("date");
            csv.WriteField("title");
            csv.WriteField("description");
            csv.WriteField("amount");
            csv.WriteField("paid_by_email");
            csv.WriteField("paid_by_alias_name");
            csv.WriteField("category");
            csv.WriteField("payment_mode");
            csv.WriteField("alias_splits");
            await csv.NextRecordAsync();

            foreach (var expense in expenses)
            {
                csv.WriteField(expense.ExpenseDate.ToString("yyyy-MM-dd"));
                csv.WriteField(expense.Title);
                csv.WriteField(expense.Description ?? string.Empty);
                csv.WriteField(expense.Amount);
                csv.WriteField(expense.PaidByUser?.Email ?? string.Empty);
                csv.WriteField(expense.PaidByAlias?.Name ?? string.Empty);
                csv.WriteField(expense.Category.ToString());
                csv.WriteField(expense.PaymentMode.ToString());

                // Format alias splits as aliasName:amount|aliasName:amount
                var aliasSplitsFormatted = string.Join("|",
                    expense.ExpenseAliasSplits.Select(eas => $"{eas.Alias.Name}:{eas.SplitAmount:F2}"));
                csv.WriteField(aliasSplitsFormatted);

                await csv.NextRecordAsync();
            }

            await writer.FlushAsync();
            var csvBytes = memoryStream.ToArray();

            logger.LogInformation("Successfully exported {Count} expenses for alias-mode group {GroupId}",
                expenses.Count, group.Id);

            return Result<byte[]>.Success(csvBytes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error exporting alias-mode expenses for group {GroupId}", group.Id);
            return Result<byte[]>.InternalServerError($"Failed to export expenses: {ex.Message}");
        }
    }
}