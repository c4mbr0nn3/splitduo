using System.Text;
using SplitDuo.Core.Dto.Imports;
using SplitDuo.Core.Services.Imports.Parser;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class SplitDuoCsvParserTests
{
    private static byte[] ToBytes(string csv) => Encoding.UTF8.GetBytes(csv);

    #region SplitDuoCsvParser Tests

    [Fact]
    public async Task Parse_ValidCsv_ReturnsExpensesAndUniqueMembers()
    {
        var csv = """
            Date,Title,Description,Amount,PaidByEmail,Category,PaymentMode,Owers
            2026-01-01,Dinner,Team dinner,42.50,alice@example.com,Food,Cash,bob@example.com:21.25|carol@example.com:21.25
            2026-01-02,Lunch,,15.00,bob@example.com,Travel,Card,alice@example.com:15.00
            """;

        var result = await SplitDuoCsvParser.ParseAsync(ToBytes(csv));

        Assert.Equal(2, result.Expenses.Count);

        var first = result.Expenses[0];
        Assert.Equal("2026-01-01", first.Date);
        Assert.Equal("Dinner", first.Title);
        Assert.Equal("Team dinner", first.Description);
        Assert.Equal(42.50m, first.Amount);
        Assert.Equal("alice@example.com", first.PaidByEmail);
        Assert.Equal("Food", first.Category);
        Assert.Equal("Cash", first.PaymentMode);
        Assert.Equal("bob@example.com:21.25|carol@example.com:21.25", first.Owers);

        var second = result.Expenses[1];
        Assert.Equal("2026-01-02", second.Date);
        Assert.Equal("Lunch", second.Title);
        Assert.Equal("", second.Description);
        Assert.Equal(15.00m, second.Amount);
        Assert.Equal("bob@example.com", second.PaidByEmail);
        Assert.Equal("Travel", second.Category);
        Assert.Equal("Card", second.PaymentMode);
        Assert.Equal("alice@example.com:15.00", second.Owers);

        // Members is the unique-email set: alice is payer + ower, bob is payer +
        // ower, carol is only an ower — the HashSet dedups them to exactly 3.
        Assert.Equal(3, result.Members.Count);
        Assert.Contains("alice@example.com", result.Members);
        Assert.Contains("bob@example.com", result.Members);
        Assert.Contains("carol@example.com", result.Members);
    }

    [Fact]
    public async Task Parse_EmptyFile_ReturnsEmptyResult()
    {
        var result = await SplitDuoCsvParser.ParseAsync([]);

        Assert.Empty(result.Expenses);
        Assert.Empty(result.Members);
    }

    #endregion
}
