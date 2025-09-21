using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using SplitDuo.Core.Dto.Imports;

namespace SplitDuo.Core.Services.Imports;

public class CospendCsvParser
{
    private static readonly List<string> MembersHeader = ["name", "weight", "active", "color"];

    private static readonly List<string> ExpensesHeader =
    [
        "what", "amount", "date", "timestamp", "payer_name", "payer_weight", "payer_active", "owers", "repeat",
        "repeatfreq", "repeatallactive", "repeatuntil", "categoryid", "paymentmode", "paymentmodeid", "comment",
        "deleted"
    ];

    private static readonly List<string> CategoriesHeader = ["categoryname", "categoryid", "icon", "color"];
    private static readonly List<string> PaymentModesHeader = ["paymentmodename", "paymentmodeid", "icon", "color"];

    private static bool IsMembersHeader(string[] header) => header.SequenceEqual(MembersHeader);
    private static bool IsExpensesHeader(string[] header) => header.SequenceEqual(ExpensesHeader);
    private static bool IsCategoriesHeader(string[] header) => header.SequenceEqual(CategoriesHeader);
    private static bool IsPaymentModesHeader(string[] header) => header.SequenceEqual(PaymentModesHeader);

    private static bool IsHeader(string[] record)
    {
        return IsMembersHeader(record)
               || IsExpensesHeader(record)
               || IsCategoriesHeader(record)
               || IsPaymentModesHeader(record);
    }

    private static CospendSection GetSection(string[] record)
    {
        if (IsMembersHeader(record)) return CospendSection.Members;
        if (IsExpensesHeader(record)) return CospendSection.Expenses;
        if (IsCategoriesHeader(record)) return CospendSection.Categories;
        if (IsPaymentModesHeader(record)) return CospendSection.PaymentModes;
        return CospendSection.None;
    }

    public static async Task<CospendParseResult> ParseAsync(string filePath, List<CospendSection> includeSections)
    {
        await using var fileStream = File.OpenRead(filePath);
        return await ParseAsync(fileStream, includeSections);
    }

    public static async Task<CospendParseResult> ParseAsync(IFormFile file, List<CospendSection> includeSections)
    {
        await using var stream = file.OpenReadStream();
        return await ParseAsync(stream, includeSections);
    }

    private static async Task<CospendParseResult> ParseAsync(Stream stream, List<CospendSection> includeSections)
    {
        using var reader = new StreamReader(stream);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

        // Configure CsvHelper to handle missing fields gracefully
        csvReader.Context.Configuration.MissingFieldFound = null;
        csvReader.Context.Configuration.HeaderValidated = null;

        var members = new List<CospendMemberDto>();
        var categories = new List<CospendCategoryDto>();
        var paymentModes = new List<CospendPaymentModeDto>();
        var expenses = new List<CospendExpenseDto>();

        var currentSection = CospendSection.None;

        while (await csvReader.ReadAsync())
        {
            var record = csvReader.Parser.Record;
            if (record == null || IsEmptyOrSectionSeparator(record))
            {
                currentSection = CospendSection.None;
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
                case CospendSection.Members:
                    if (!includeSections.Contains(CospendSection.Members)) continue;
                    var member = csvReader.GetRecord<CospendMemberDto>();
                    members.Add(member);
                    break;
                case CospendSection.Expenses:
                    if (!includeSections.Contains(CospendSection.Expenses)) continue;
                    var expense = csvReader.GetRecord<CospendExpenseDto>();
                    expenses.Add(expense);
                    break;
                case CospendSection.Categories:
                    if (!includeSections.Contains(CospendSection.Categories)) continue;
                    var category = csvReader.GetRecord<CospendCategoryDto>();
                    categories.Add(category);
                    break;
                case CospendSection.PaymentModes:
                    if (!includeSections.Contains(CospendSection.PaymentModes)) continue;
                    var paymentMode = csvReader.GetRecord<CospendPaymentModeDto>();
                    paymentModes.Add(paymentMode);
                    break;
                case CospendSection.None:
                    // Skip lines until we find a section header
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentSection), "Unexpected section type");
            }
        }

        return new CospendParseResult
        {
            Members = members,
            Categories = categories,
            PaymentModes = paymentModes,
            Expenses = expenses
        };
    }

    private static bool IsEmptyOrSectionSeparator(string[] record) =>
        record.Length == 0 || record.All(string.IsNullOrWhiteSpace);
}

public class CospendParseResult
{
    public List<CospendMemberDto> Members { get; set; } = [];
    public List<CospendCategoryDto> Categories { get; set; } = [];
    public List<CospendPaymentModeDto> PaymentModes { get; set; } = [];
    public List<CospendExpenseDto> Expenses { get; set; } = [];
}