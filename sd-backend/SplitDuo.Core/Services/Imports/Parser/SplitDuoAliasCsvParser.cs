using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports.Parser;

// TODO: this could be a service with DI
public static class SplitDuoAliasCsvParser
{
    private static readonly List<string> AliasesHeader = ["name", "is_singleton"];

    private static readonly List<string> MembersHeader = ["email", "alias_name", "role"];

    private static readonly List<string> ExpensesHeader =
    [
        "date", "title", "description", "amount", "paid_by_email", "paid_by_alias_name", "category", "payment_mode",
        "alias_splits"
    ];

    private static bool IsAliasesHeader(string[] header) => header.SequenceEqual(AliasesHeader);
    private static bool IsMembersHeader(string[] header) => header.SequenceEqual(MembersHeader);
    private static bool IsExpensesHeader(string[] header) => header.SequenceEqual(ExpensesHeader);

    private static bool IsHeader(string[] record)
    {
        return IsAliasesHeader(record)
               || IsMembersHeader(record)
               || IsExpensesHeader(record);
    }

    private static SplitDuoAliasSection GetSection(string[] record)
    {
        if (IsAliasesHeader(record)) return SplitDuoAliasSection.Aliases;
        if (IsMembersHeader(record)) return SplitDuoAliasSection.Members;
        if (IsExpensesHeader(record)) return SplitDuoAliasSection.Expenses;
        return SplitDuoAliasSection.None;
    }

    public static async Task<SplitDuoAliasParseResult> ParseAsync(byte[] file)
    {
        await using var fileStream = new MemoryStream(file);
        return await ParseAsync(fileStream);
    }

    public static async Task<SplitDuoAliasParseResult> ParseAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        return await ParseAsync(stream);
    }

    private static async Task<SplitDuoAliasParseResult> ParseAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null,
        };
        using var csvReader = new CsvReader(reader, config);

        var aliases = new List<SplitDuoAliasDto>();
        var members = new List<SplitDuoAliasMemberDto>();
        var expenses = new List<SplitDuoAliasExpenseDto>();

        var currentSection = SplitDuoAliasSection.None;

        while (await csvReader.ReadAsync())
        {
            var record = csvReader.Parser.Record;
            if (record == null || IsEmptyOrSectionSeparator(record))
            {
                currentSection = SplitDuoAliasSection.None;
                continue;
            }

            var isHeader = IsHeader(record);
            if (isHeader)
            {
                currentSection = GetSection(record);
                csvReader.ReadHeader();
                continue;
            }

            switch (currentSection)
            {
                case SplitDuoAliasSection.Aliases:
                    var alias = csvReader.GetRecord<SplitDuoAliasDto>();
                    aliases.Add(alias);
                    break;
                case SplitDuoAliasSection.Members:
                    var member = csvReader.GetRecord<SplitDuoAliasMemberDto>();
                    members.Add(member);
                    break;
                case SplitDuoAliasSection.Expenses:
                    var expense = csvReader.GetRecord<SplitDuoAliasExpenseDto>();
                    expenses.Add(expense);
                    break;
                case SplitDuoAliasSection.None:
                    // Skip lines until we find a section header
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentSection), "Unexpected section type");
            }
        }

        return new SplitDuoAliasParseResult
        {
            Aliases = aliases,
            Members = members,
            Expenses = expenses
        };
    }

    private static bool IsEmptyOrSectionSeparator(string[] record) =>
        record.Length == 0 || record.All(string.IsNullOrWhiteSpace);
}
