using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;

namespace SplitDuo.Tests.Integration.Support;

/// <summary>
/// HttpClient extensions for Expenses feature test setup.
/// </summary>
public static class ExpenseTestExtensions
{
    /// <summary>
    /// Creates an expense via POST /api/v1/groups/{groupId}/expenses and returns the ExpenseDto.
    /// When splits is null, auto-builds a single split where the payer pays the full amount.
    /// </summary>
    public static async Task<ExpenseDto> CreateExpenseAsync(
        this HttpClient client,
        string groupId,
        string paidByUserId,
        decimal amount = 10m,
        string title = "Test Expense",
        int categoryId = 1,
        int paymentModeId = 1,
        string expenseDate = "2025-01-15",
        object? splits = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var splitsValue = splits ?? new[] { new { userId = paidByUserId, splitAmount = amount } };
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/expenses", new
        {
            title,
            amount,
            paidByUserId,
            expenseDate,
            categoryId,
            paymentModeId,
            splits = splitsValue,
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        return body!.Data!;
    }

    /// <summary>
    /// Creates an alias-mode expense via POST /api/v1/groups/{groupId}/expenses with alias splits.
    /// </summary>
    public static async Task<ExpenseDto> CreateAliasExpenseAsync(
        this HttpClient client,
        string groupId,
        string paidByUserId,
        decimal amount,
        object aliasSplits,
        string title = "Test Expense",
        int categoryId = 1,
        int paymentModeId = 1,
        string expenseDate = "2025-01-15")
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await client.PostAsJsonAsync($"/api/v1/groups/{groupId}/expenses", new
        {
            title,
            amount,
            paidByUserId,
            expenseDate,
            categoryId,
            paymentModeId,
            aliasSplits,
        }, ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        return body!.Data!;
    }
}
