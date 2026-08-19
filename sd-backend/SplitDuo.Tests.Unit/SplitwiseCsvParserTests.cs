using System.Text;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Services.Imports.Parser;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class SplitwiseCsvParserTests
{
    #region SplitwiseCsvParser Tests

    #region Happy Path

    [Fact]
    public async Task Parse_ValidCsv_ReturnsExpensesMembersAndCategories()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,"Dinner at trattoria",Food,42.00,EUR,42.00,-42.00
            2026-01-07,"Train tickets",Travel,120.00,EUR,-120.00,120.00
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Equal(2, result.Expenses.Count);
        Assert.Equal(2, result.Members.Count);
        Assert.Contains("Alice", result.Members);
        Assert.Contains("Bob", result.Members);

        // First expense
        var first = result.Expenses[0];
        Assert.Equal(new DateOnly(2026, 1, 5), first.Date);
        Assert.Equal("Dinner at trattoria", first.Description);
        Assert.Equal("Food", first.Category);
        Assert.Equal(42.00m, first.Cost);
        Assert.Equal("Alice", first.PayerName);
        Assert.Single(first.Owers);
        Assert.Equal("Bob", first.Owers[0].Name);
        Assert.Equal(42.00m, first.Owers[0].Amount);

        // Second expense
        var second = result.Expenses[1];
        Assert.Equal(new DateOnly(2026, 1, 7), second.Date);
        Assert.Equal("Train tickets", second.Description);
        Assert.Equal("Travel", second.Category);
        Assert.Equal(120.00m, second.Cost);
        Assert.Equal("Bob", second.PayerName);
        Assert.Single(second.Owers);
        Assert.Equal("Alice", second.Owers[0].Name);
        Assert.Equal(120.00m, second.Owers[0].Amount);

        // Categories in first-seen order with sequential keys
        Assert.Equal(2, result.Categories.Count);
        Assert.Equal("0", result.Categories[0].Key);
        Assert.Equal("Food", result.Categories[0].Value);
        Assert.Equal("1", result.Categories[1].Key);
        Assert.Equal("Travel", result.Categories[1].Value);
    }

    #endregion

    #region Header Handling

    [Fact]
    public async Task Parse_EmptyFile_ReturnsEmptyResult()
    {
        var result = await SplitwiseCsvParser.ParseAsync([]);

        Assert.Empty(result.Expenses);
        Assert.Empty(result.Members);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Parse_HeaderOnly_ReturnsEmptyExpenses()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Empty(result.Expenses);
        Assert.Equal(2, result.Members.Count);
        Assert.Contains("Alice", result.Members);
        Assert.Contains("Bob", result.Members);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Parse_FewerThanSixHeaders_ReturnsEmptyResult()
    {
        var csv = """
            Date,Description,Category,Cost,Currency
            2026-01-05,Dinner,Food,42.00,EUR
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        // Header check fails before participants are extracted, so Members is NOT populated
        Assert.Empty(result.Expenses);
        Assert.Empty(result.Members);
        Assert.Empty(result.Categories);
    }

    #endregion

    #region Row Skipping

    [Fact]
    public async Task Parse_SkipsZeroCostRows()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,42.00,EUR,42.00,0
            2026-01-31,Total balance,Total,0,EUR,0,0
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Single(result.Expenses);
        Assert.Equal("Dinner", result.Expenses[0].Description);

        // Zero-cost skip happens before category registration
        Assert.Single(result.Categories);
        Assert.Equal("Food", result.Categories[0].Value);
    }

    [Fact]
    public async Task Parse_SkipsUnparseableCostRows()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,abc,EUR,42.00,0
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Empty(result.Expenses);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Parse_SkipsRowsWithMultiplePayers()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,42.00,EUR,20.00,22.00
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Empty(result.Expenses);

        // Payer-null check happens before category registration
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Parse_SkipsRowsWithNoPayer()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,42.00,EUR,0,-42.00
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Empty(result.Expenses);
        Assert.Empty(result.Categories);
    }

    #endregion

    #region Field Parsing

    [Fact]
    public async Task Parse_HandlesQuotedFieldsWithCommas()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,"Lunch, drinks",Food,25.00,EUR,25.00,0
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        var expense = Assert.Single(result.Expenses);
        Assert.Equal("Lunch, drinks", expense.Description);
        Assert.Equal(new DateOnly(2026, 1, 5), expense.Date);
        Assert.Equal("Food", expense.Category);
        Assert.Equal(25.00m, expense.Cost);
        Assert.Equal("Alice", expense.PayerName);
    }

    [Fact]
    public async Task Parse_NegativeOwerAmounts_StoredAsAbsolute()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,42.00,EUR,42.00,-15.50
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        var expense = Assert.Single(result.Expenses);
        var ower = Assert.Single(expense.Owers);
        Assert.Equal("Bob", ower.Name);
        Assert.Equal(15.50m, ower.Amount);
    }

    #endregion

    #region Categories

    [Fact]
    public async Task Parse_PreservesCategoryOrder()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            2026-01-05,Dinner,Food,42.00,EUR,42.00,0
            2026-01-06,Hotel,Travel,90.00,EUR,90.00,0
            2026-01-07,Lunch,food,15.00,EUR,15.00,0
            2026-01-08,Market,Groceries,30.00,EUR,30.00,0
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        Assert.Equal(4, result.Expenses.Count);

        // First-seen order preserved, dedup is case-insensitive ("food" matches "Food")
        Assert.Equal(3, result.Categories.Count);
        Assert.Equal("0", result.Categories[0].Key);
        Assert.Equal("Food", result.Categories[0].Value);
        Assert.Equal("1", result.Categories[1].Key);
        Assert.Equal("Travel", result.Categories[1].Value);
        Assert.Equal("2", result.Categories[2].Key);
        Assert.Equal("Groceries", result.Categories[2].Value);
    }

    [Fact]
    public async Task Parse_SkipsUnparseableDates()
    {
        var csv = """
            Date,Description,Category,Cost,Currency,Alice,Bob
            not-a-date,Dinner,Food,42.00,EUR,42.00,0
            """;

        var result = await SplitwiseCsvParser.ParseAsync(Encoding.UTF8.GetBytes(csv));

        // Expense is NOT created because the date is unparseable...
        Assert.Empty(result.Expenses);

        // ...BUT the category IS still registered: the category is added to
        // categoryOrder BEFORE the date check, so an unparseable date skips the
        // expense while still registering its category in the result.
        Assert.Single(result.Categories);
        Assert.Equal("Food", result.Categories[0].Value);
    }

    #endregion

    #endregion
}
