using System.Text;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Services.Imports.Parser;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class SplitDuoAliasCsvParserTests
{
    private static byte[] ToBytes(string csv) => Encoding.UTF8.GetBytes(csv);

    #region SplitDuoAliasCsvParser Tests

    #region Section Parsing

    [Fact]
    public async Task Parse_AliasesSection_ReturnsAliases()
    {
        var csv = """
            name,is_singleton
            Groceries,0
            Rent,1
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        Assert.Equal(2, result.Aliases.Count);
        Assert.Equal("Groceries", result.Aliases[0].Name);
        Assert.Equal(0, result.Aliases[0].IsSingletonInt);
        Assert.Equal("Rent", result.Aliases[1].Name);
        Assert.Equal(1, result.Aliases[1].IsSingletonInt);

        Assert.Empty(result.Members);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_MembersSection_ReturnsMembers()
    {
        var csv = """
            email,alias_name,role
            alice@example.com,Alice,admin
            bob@example.com,Bob,member
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        Assert.Equal(2, result.Members.Count);
        Assert.Equal("alice@example.com", result.Members[0].Email);
        Assert.Equal("Alice", result.Members[0].AliasName);
        Assert.Equal("admin", result.Members[0].Role);
        Assert.Equal("bob@example.com", result.Members[1].Email);
        Assert.Equal("Bob", result.Members[1].AliasName);
        Assert.Equal("member", result.Members[1].Role);

        Assert.Empty(result.Aliases);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_ExpensesSection_ReturnsExpenses()
    {
        var csv = """
            date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
            2026-01-01,Dinner,Team dinner,42.50,alice@example.com,Alice,Food,Cash,Alice:21.25|Bob:21.25
            2026-01-02,Metro,Commute,8.00,bob@example.com,Bob,Travel,Card,Bob:8.00
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        Assert.Equal(2, result.Expenses.Count);

        var first = result.Expenses[0];
        Assert.Equal("2026-01-01", first.Date);
        Assert.Equal("Dinner", first.Title);
        Assert.Equal("Team dinner", first.Description);
        Assert.Equal(42.50m, first.Amount);
        Assert.Equal("alice@example.com", first.PaidByEmail);
        Assert.Equal("Alice", first.PaidByAliasName);
        Assert.Equal("Food", first.Category);
        Assert.Equal("Cash", first.PaymentMode);
        Assert.Equal("Alice:21.25|Bob:21.25", first.AliasSplits);

        var second = result.Expenses[1];
        Assert.Equal("2026-01-02", second.Date);
        Assert.Equal("Metro", second.Title);
        Assert.Equal("Commute", second.Description);
        Assert.Equal(8.00m, second.Amount);
        Assert.Equal("bob@example.com", second.PaidByEmail);
        Assert.Equal("Bob", second.PaidByAliasName);
        Assert.Equal("Travel", second.Category);
        Assert.Equal("Card", second.PaymentMode);
        Assert.Equal("Bob:8.00", second.AliasSplits);

        Assert.Empty(result.Aliases);
        Assert.Empty(result.Members);
    }

    [Fact]
    public async Task Parse_ThreeSections_ReturnsAll()
    {
        var csv = """
            name,is_singleton
            Groceries,0
            Rent,1

            email,alias_name,role
            alice@example.com,Alice,admin
            bob@example.com,Bob,member

            date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
            2026-01-01,Dinner,Team dinner,42.50,alice@example.com,Alice,Food,Cash,Alice:21.25|Bob:21.25
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        // All three sections are parsed unconditionally (no include filter)
        Assert.Equal(2, result.Aliases.Count);
        Assert.Equal("Groceries", result.Aliases[0].Name);
        Assert.Equal("Rent", result.Aliases[1].Name);

        Assert.Equal(2, result.Members.Count);
        Assert.Equal("alice@example.com", result.Members[0].Email);
        Assert.Equal("bob@example.com", result.Members[1].Email);

        var expense = Assert.Single(result.Expenses);
        Assert.Equal("Dinner", expense.Title);
        Assert.Equal(42.50m, expense.Amount);
        Assert.Equal("alice@example.com", expense.PaidByEmail);
        Assert.Equal("Food", expense.Category);
        Assert.Equal("Cash", expense.PaymentMode);
        Assert.Equal("Alice:21.25|Bob:21.25", expense.AliasSplits);
    }

    #endregion

    #region Section State

    [Fact]
    public async Task Parse_EmptyOrSeparatorLines_ResetSection()
    {
        // NOTE: CsvHelper's default IgnoreBlankLines=true means blank lines never
        // reach the parser, so the section reset is only observable with a line of
        // all-empty fields (e.g. ",,"), which IsEmptyOrSectionSeparator detects.
        var csv = """
            name,is_singleton
            Groceries,0
            ,,
            orphan,row,data
            email,alias_name,role
            alice@example.com,Alice,admin
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        // The ",," separator resets the section to None, so the orphan row is
        // skipped; the members header re-establishes the section.
        Assert.Single(result.Aliases);
        Assert.Equal("Groceries", result.Aliases[0].Name);
        Assert.Single(result.Members);
        Assert.Equal("alice@example.com", result.Members[0].Email);
        Assert.Empty(result.Expenses);
    }

    [Fact]
    public async Task Parse_MissingFields_DoesNotThrow()
    {
        // MissingFieldFound = null is set via CsvConfiguration (at construction),
        // so rows with fewer fields than the header are parsed leniently — missing
        // fields get default values instead of throwing MissingFieldException.
        var csv = """
            date,title,description,amount,paid_by_email,paid_by_alias_name,category,payment_mode,alias_splits
            2026-01-01,Dinner,Team dinner,42.50,alice@example.com,Alice,Food
            """;

        var result = await SplitDuoAliasCsvParser.ParseAsync(ToBytes(csv));

        // Parsing completes without throwing; the expense is parsed with default
        // values for the missing trailing fields (payment_mode="", alias_splits="").
        var expense = Assert.Single(result.Expenses);
        Assert.Equal("Dinner", expense.Title);
        Assert.Equal(42.50m, expense.Amount);
        Assert.Equal("", expense.PaymentMode);
        Assert.Equal("", expense.AliasSplits);
    }

    #endregion

    #endregion
}
