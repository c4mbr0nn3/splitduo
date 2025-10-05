using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports.Parser;

public static class SplitDuoCsvParser
{
    public static async Task<SplitDuoParseResult> ParseAsync(byte[] file)
    {
        await using var fileStream = new MemoryStream(file);
        return await ParseAsync(fileStream);
    }

    public static async Task<SplitDuoParseResult> ParseAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await ParseAsync(stream);
    }

    private static async Task<SplitDuoParseResult> ParseAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Configure CsvHelper to handle missing fields gracefully
        csvReader.Context.Configuration.MissingFieldFound = null;
        csvReader.Context.Configuration.HeaderValidated = null;

        var expenses = new List<SplitDuoExpenseDto>();
        var uniqueEmails = new HashSet<string>();

        // Read all expense records
        await foreach (var expense in csvReader.GetRecordsAsync<SplitDuoExpenseDto>())
        {
            expenses.Add(expense);

            // Collect unique emails
            uniqueEmails.Add(expense.PaidByEmail);
            foreach (var owner in expense.ParsedOwers)
            {
                uniqueEmails.Add(owner.Email);
            }
        }

        return new SplitDuoParseResult
        {
            Expenses = expenses,
            UniqueEmails = uniqueEmails
        };
    }
}