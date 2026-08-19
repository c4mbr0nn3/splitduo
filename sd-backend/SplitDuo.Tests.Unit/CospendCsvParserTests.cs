using System.Text;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Services.Imports.Parser;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class CospendCsvParserTests
{
    private static byte[] ToBytes(string csv) => Encoding.UTF8.GetBytes(csv);

    #region CospendCsvParser Tests

    #region Section Parsing

    [Fact]
    public async Task Parse_MembersSection_ReturnsMembers()
    {
        var csv = """
            name,weight,active,color
            Alice,1,1,#ff0000
            Bob,2,0,#00ff00
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Members]);

        Assert.Equal(2, result.Members.Count);
        Assert.Equal("Alice", result.Members[0].Name);
        Assert.Equal(1, result.Members[0].Weight);
        Assert.Equal(1, result.Members[0].Active);
        Assert.Equal("#ff0000", result.Members[0].Color);
        Assert.Equal("Bob", result.Members[1].Name);
        Assert.Equal(2, result.Members[1].Weight);
        Assert.Equal(0, result.Members[1].Active);
        Assert.Equal("#00ff00", result.Members[1].Color);

        Assert.Empty(result.Categories);
        Assert.Empty(result.PaymentModes);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_CategoriesSection_ReturnsCategories()
    {
        var csv = """
            categoryname,categoryid,icon,color
            Food,1,utensils,#ff0000
            Travel,2,plane,#00ff00
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Categories]);

        Assert.Equal(2, result.Categories.Count);
        Assert.Equal("Food", result.Categories[0].CategoryName);
        Assert.Equal(1, result.Categories[0].CategoryId);
        Assert.Equal("utensils", result.Categories[0].Icon);
        Assert.Equal("#ff0000", result.Categories[0].Color);
        Assert.Equal("Travel", result.Categories[1].CategoryName);
        Assert.Equal(2, result.Categories[1].CategoryId);
        Assert.Equal("plane", result.Categories[1].Icon);
        Assert.Equal("#00ff00", result.Categories[1].Color);

        Assert.Empty(result.Members);
        Assert.Empty(result.PaymentModes);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_PaymentModesSection_ReturnsPaymentModes()
    {
        var csv = """
            paymentmodename,paymentmodeid,icon,color
            Cash,1,,#000000
            Card,2,,#ffffff
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.PaymentModes]);

        Assert.Equal(2, result.PaymentModes.Count);
        Assert.Equal("Cash", result.PaymentModes[0].PaymentModeName);
        Assert.Equal(1, result.PaymentModes[0].PaymentModeId);
        Assert.Equal("", result.PaymentModes[0].Icon);
        Assert.Equal("#000000", result.PaymentModes[0].Color);
        Assert.Equal("Card", result.PaymentModes[1].PaymentModeName);
        Assert.Equal(2, result.PaymentModes[1].PaymentModeId);
        Assert.Equal("", result.PaymentModes[1].Icon);
        Assert.Equal("#ffffff", result.PaymentModes[1].Color);

        Assert.Empty(result.Members);
        Assert.Empty(result.Categories);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_ExpensesSection_ReturnsExpenses()
    {
        var csv = """
            what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
            Dinner,25.50,2026-01-01,1767225600,Alice,1,1,Bob,0,0,0,,1,c,1,,0
            Lunch,12.00,2026-01-02,1767312000,Bob,1,1,Alice,0,0,0,,2,c,1,,0
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Expenses]);

        Assert.Equal(2, result.Expenses.Count);

        var first = result.Expenses[0];
        Assert.Equal("Dinner", first.What);
        Assert.Equal(25.50m, first.Amount);
        Assert.Equal("2026-01-01", first.Date);
        Assert.Equal(1767225600L, first.Timestamp);
        Assert.Equal("Alice", first.PayerName);
        Assert.Equal("Bob", first.Owers);
        Assert.Equal(1, first.CategoryId);
        Assert.Equal(1, first.PaymentModeId);
        Assert.Equal("", first.Comment);
        Assert.Equal(0, first.Deleted);

        var second = result.Expenses[1];
        Assert.Equal("Lunch", second.What);
        Assert.Equal(12.00m, second.Amount);
        Assert.Equal("2026-01-02", second.Date);
        Assert.Equal(1767312000L, second.Timestamp);
        Assert.Equal("Bob", second.PayerName);
        Assert.Equal("Alice", second.Owers);
        Assert.Equal(2, second.CategoryId);
        Assert.Equal(1, second.PaymentModeId);
        Assert.Equal("", second.Comment);
        Assert.Equal(0, second.Deleted);

        Assert.Empty(result.Members);
        Assert.Empty(result.Categories);
        Assert.Empty(result.PaymentModes);
    }

    #endregion

    #region Section Filtering & State

    [Fact]
    public async Task Parse_IncludeSectionsFilter_OnlyParsesRequestedSections()
    {
        var csv = """
            name,weight,active,color
            Alice,1,1,#ff0000
            Bob,1,1,#00ff00

            categoryname,categoryid,icon,color
            Food,1,utensils,#ff0000

            paymentmodename,paymentmodeid,icon,color
            Cash,1,,#000000

            what,amount,date,timestamp,payer_name,payer_weight,payer_active,owers,repeat,repeatfreq,repeatallactive,repeatuntil,categoryid,paymentmode,paymentmodeid,comment,deleted
            Dinner,25.50,2026-01-01,1767225600,Alice,1,1,Bob,0,0,0,,1,c,1,,0
            Lunch,12.00,2026-01-02,1767312000,Bob,1,1,Alice,0,0,0,,2,c,1,,0
            """;

        var result = await CospendCsvParser.ParseAsync(
            ToBytes(csv), [CospendSection.Members, CospendSection.Expenses]);

        // Requested sections are parsed
        Assert.Equal(2, result.Members.Count);
        Assert.Equal(2, result.Expenses.Count);

        // Excluded sections are skipped entirely
        Assert.Empty(result.Categories);
        Assert.Empty(result.PaymentModes);
    }

    [Fact]
    public async Task Parse_EmptyOrSeparatorLines_ResetSection()
    {
        // NOTE: CsvHelper's default IgnoreBlankLines=true means blank lines never
        // reach the parser, so the section reset is only observable with a line of
        // all-empty fields (e.g. ",,,"), which IsEmptyOrSectionSeparator detects.
        var csv = """
            name,weight,active,color
            Alice,1,1,#ff0000
            ,,,
            orphan,row,data,here
            name,weight,active,color
            Bob,1,1,#00ff00
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Members]);

        // The ",,," separator resets the section to None, so the orphan row is
        // skipped; the second members header re-establishes the section.
        Assert.Equal(2, result.Members.Count);
        Assert.Equal("Alice", result.Members[0].Name);
        Assert.Equal("Bob", result.Members[1].Name);
    }

    [Fact]
    public async Task Parse_MissingFields_DoesNotThrow()
    {
        // MissingFieldFound = null is set via CsvConfiguration (at construction),
        // so rows with fewer fields than the header are parsed leniently — missing
        // fields get default values instead of throwing MissingFieldException.
        // (Uses a members row: the expenses DTO's trailing `deleted` field is a
        // non-nullable int, and CsvHelper still throws TypeConverterException
        // when a non-nullable value-type field is missing.)
        var csv = """
            name,weight,active,color
            Alice,1,1
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Members]);

        // Parsing completes without throwing; the member is parsed with a default
        // value for the missing trailing field (color="").
        var member = Assert.Single(result.Members);
        Assert.Equal("Alice", member.Name);
        Assert.Equal("", member.Color);
    }

    [Fact]
    public async Task Parse_UnknownSection_SkipsUntilHeader()
    {
        var csv = """
            orphan,row,data,here
            name,weight,active,color
            Alice,1,1,#ff0000
            """;

        var result = await CospendCsvParser.ParseAsync(ToBytes(csv), [CospendSection.Members]);

        // The orphan row before any section header is skipped (currentSection == None)
        Assert.Single(result.Members);
        Assert.Equal("Alice", result.Members[0].Name);
    }

    #endregion

    #endregion
}
