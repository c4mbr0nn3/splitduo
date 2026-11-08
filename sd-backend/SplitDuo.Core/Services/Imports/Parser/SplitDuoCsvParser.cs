using CsvHelper;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Common;
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
        // CsvHelper 34 recovers from malformed records and missing fields by default;
        // every DTO column is marked optional, so a missing column is not an error.
        using var csvReader = CsvReader.Create(reader);

        var expenses = new List<SplitDuoExpenseDto>();
        var uniqueEmails = new HashSet<string>();

        // Read all expense records
        await foreach (var expense in csvReader.GetRecordsAsync<SplitDuoExpenseDto>())
        {
            expenses.Add(expense);

            // Collect unique emails in the same normalized form the users table stores.
            uniqueEmails.Add(EmailNormalizer.Normalize(expense.PaidByEmail));
            foreach (var owner in expense.ParsedOwers)
            {
                uniqueEmails.Add(EmailNormalizer.Normalize(owner.Email));
            }
        }

        return new SplitDuoParseResult
        {
            Expenses = expenses,
            Members = uniqueEmails
        };
    }
}