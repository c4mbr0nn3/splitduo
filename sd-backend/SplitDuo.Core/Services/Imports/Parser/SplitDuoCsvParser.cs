using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports.Parser;

// TODO: this could be a service with DI
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
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
        };
        using var csvReader = new CsvReader(reader, config);

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
            Members = uniqueEmails
        };
    }
}