using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Tests.Integration.Support;

namespace SplitDuo.Tests.Integration;

public class ExpensesTests : IntegrationTest
{
    public ExpensesTests(SplitDuoApiFactory factory) : base(factory) { }

    #region Happy paths

    [Fact]
    public async Task CreateExpense_Returns200_WithExpenseData()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var expense = await client.CreateExpenseAsync(group.Id, adminId, amount: 50m);

        Assert.Equal("Test Expense", expense.Title);
        Assert.Equal(50m, expense.Amount);
        Assert.Equal(adminId, expense.PaidByUserId);
        Assert.Equal(adminId, expense.PaidByUser.Id);
        Assert.Equal(1, expense.CategoryId);
        Assert.Equal(1, expense.PaymentModeId);
        Assert.Equal("2025-01-15", expense.ExpenseDate);
        Assert.Single(expense.Splits);
        Assert.Equal(adminId, expense.Splits[0].UserId);
        Assert.Equal(50m, expense.Splits[0].SplitAmount);
        Assert.Equal(100m, expense.Splits[0].SplitPercentage);
        Assert.True(Guid.TryParse(expense.Id, out _));
        Assert.True(Guid.TryParse(expense.GroupId, out _));
        Assert.Equal(group.Id, expense.GroupId);
        Assert.True(expense.CreatedAt > 0);
        Assert.True(expense.CreatedAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
        Assert.True(expense.UpdatedAt > 0);
        Assert.True(expense.UpdatedAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
    }

    [Fact]
    public async Task CreateExpense_AppearsInList()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.NotNull(body!.Data);
        Assert.Single(body.Data);
        Assert.Equal(expense.Id, body.Data[0].Id);
        Assert.Equal(1, body.Pagination.Page);
        Assert.Equal(20, body.Pagination.Limit);
        Assert.Equal(1, body.Pagination.Total);
        Assert.Equal(1, body.Pagination.TotalPages);
        Assert.False(body.Pagination.HasNext);
        Assert.False(body.Pagination.HasPrev);
    }

    [Fact]
    public async Task GetExpenseById_ReturnsExpense()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal(expense.Id, body!.Data!.Id);
        Assert.Equal(expense.Title, body.Data.Title);
        Assert.Equal(expense.Amount, body.Data.Amount);
    }

    [Fact]
    public async Task UpdateExpense_Title_PersistsAndReturnsUpdated()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var putResponse = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", new
        {
            title = "Updated",
            categoryId = 1,
            paymentModeId = 1,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var putBody = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Updated", putBody!.Data!.Title);

        // Confirm via GET
        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);
        getResponse.EnsureSuccessStatusCode();
        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Updated", getBody!.Data!.Title);
    }

    [Fact]
    public async Task DeleteExpense_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteExpense_RemovesFromList_AndGetByIdReturns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);
        await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);

        var listResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Empty(listBody!.Data!);

        var getResponse = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Expense not found", getBody!.Error!.Message);
    }

    #endregion

    #region Splits validation

    [Fact]
    public async Task CreateExpense_NoSplits_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = Array.Empty<object>(),
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("At least one expense split is required", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_SplitsDoNotSum_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 50m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 48m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Split amounts (48.00) do not sum up to expense amount (50.00)", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_DuplicateUsersInSplits_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 20m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[]
            {
                new { userId = adminId, splitAmount = 10m },
                new { userId = adminId, splitAmount = 10m },
            },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal($"Duplicate users found in splits: {adminId}", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_SplitAmountZero_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 0m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Split amount must be greater than zero", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_SplitUserNotMember_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme");
        var user2 = await user2Client.GetCurrentUserAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = group.CreatedByUserId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = user2.Id, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("User Second User is not a member of this group", body!.Error!.Message);
    }

    #endregion

    #region Create validation

    [Fact]
    public async Task CreateExpense_MissingTitle_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_AmountZero_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 0m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 0m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_InvalidCategoryId_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 99,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid expense category", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_InvalidPaymentModeId_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 99,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid expense payment mode", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_InvalidPaidByUserId_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = "not-a-guid",
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = group.CreatedByUserId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid paid by user ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_InvalidExpenseDate_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = adminId,
            expenseDate = "not-a-date",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid expense date format", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_PaidByUserNotFound_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = Guid.NewGuid().ToString(),
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = group.CreatedByUserId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Paid by user not found", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_PaidByUserNotMember_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme");
        var user2 = await user2Client.GetCurrentUserAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = user2.Id,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = adminId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Paid by user is not a member of this group", body!.Error!.Message);
    }

    #endregion

    #region Invalid Guid format

    [Fact]
    public async Task ListExpenses_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/groups/not-a-guid/expenses", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_InvalidGroupGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/groups/not-a-guid/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = Guid.NewGuid().ToString(),
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = Array.Empty<object>(),
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid group ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task GetExpense_InvalidExpenseGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid expense ID format", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteExpense_InvalidExpenseGuid_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/not-a-guid", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Invalid expense ID format", body!.Error!.Message);
    }

    #endregion

    #region Auth — 401 unauthenticated

    [Fact]
    public async Task ListExpenses_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/expenses", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync($"/api/v1/groups/{Guid.NewGuid()}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = Guid.NewGuid().ToString(),
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = Array.Empty<object>(),
        }, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Not found — 404

    [Fact]
    public async Task ListExpenses_NonexistentGroup_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/v1/groups/{Guid.NewGuid()}/expenses", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Group not found", body!.Error!.Message);
    }

    [Fact]
    public async Task GetExpense_NonexistentExpense_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Expense not found", body!.Error!.Message);
    }

    [Fact]
    public async Task DeleteExpense_NonexistentExpense_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();

        var response = await client.DeleteAsync($"/api/v1/groups/{group.Id}/expenses/{Guid.NewGuid()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(ct);
        Assert.Equal("Expense not found", body!.Error!.Message);
    }

    #endregion

    #region Not a member — 403

    [Fact]
    public async Task ListExpenses_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme");
        var user2Group = await user2Client.CreateGroupAsync();

        var response = await adminClient.GetAsync($"/api/v1/groups/{user2Group.Id}/expenses", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task CreateExpense_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme");
        var user2Group = await user2Client.CreateGroupAsync();

        var response = await adminClient.PostAsJsonAsync($"/api/v1/groups/{user2Group.Id}/expenses", new
        {
            title = "Test",
            amount = 10m,
            paidByUserId = user2Group.CreatedByUserId,
            expenseDate = "2025-01-15",
            categoryId = 1,
            paymentModeId = 1,
            splits = new[] { new { userId = user2Group.CreatedByUserId, splitAmount = 10m } },
        }, ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    [Fact]
    public async Task GetExpense_NotAMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminClient = await CreateAuthenticatedClientAsync();

        var user2Email = await TestDbSeeder.SeedUserAsync(Factory.Services);
        var user2Client = await CreateAuthenticatedClientAsync(user2Email, "changeme");
        var user2Group = await user2Client.CreateGroupAsync();
        var user2Expense = await user2Client.CreateExpenseAsync(user2Group.Id, user2Group.CreatedByUserId);

        var response = await adminClient.GetAsync($"/api/v1/groups/{user2Group.Id}/expenses/{user2Expense.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Access to this group is not allowed", body!.Error!.Message);
    }

    #endregion

    #region Pagination / filters

    [Fact]
    public async Task ListExpenses_Pagination_ReturnsCorrectPage()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        await client.CreateExpenseAsync(group.Id, adminId, expenseDate: "2025-01-10");
        await client.CreateExpenseAsync(group.Id, adminId, expenseDate: "2025-01-15");
        await client.CreateExpenseAsync(group.Id, adminId, expenseDate: "2025-01-20");

        var page1Response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses?page=1&limit=2", ct);
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        var page1 = await page1Response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Equal(2, page1!.Data.Count);
        Assert.Equal(3, page1.Pagination.Total);
        Assert.Equal(2, page1.Pagination.TotalPages);
        Assert.True(page1.Pagination.HasNext);
        Assert.False(page1.Pagination.HasPrev);
        // Ordering: DESC by date — page 1 has Jan 20 + Jan 15
        Assert.Equal("2025-01-20", page1.Data[0].ExpenseDate);
        Assert.Equal("2025-01-15", page1.Data[1].ExpenseDate);

        var page2Response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses?page=2&limit=2", ct);
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
        var page2 = await page2Response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Single(page2!.Data);
        Assert.False(page2.Pagination.HasNext);
        Assert.True(page2.Pagination.HasPrev);
        Assert.Equal("2025-01-10", page2.Data[0].ExpenseDate);
    }

    [Fact]
    public async Task ListExpenses_FilterByCategory_ReturnsMatchingExpenses()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        await client.CreateExpenseAsync(group.Id, adminId, categoryId: 2); // Groceries
        await client.CreateExpenseAsync(group.Id, adminId, categoryId: 3); // Transportation

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses?category=Groceries", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Single(body!.Data);
        Assert.Equal(2, body.Data[0].CategoryId);
    }

    [Fact]
    public async Task ListExpenses_FilterBySearch_ReturnsMatchingExpenses()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;

        await client.CreateExpenseAsync(group.Id, adminId, title: "Grocery run");
        await client.CreateExpenseAsync(group.Id, adminId, title: "Bus ticket");

        var response = await client.GetAsync($"/api/v1/groups/{group.Id}/expenses?search=grocery", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponseDto<ExpenseDto>>(ct);
        Assert.Single(body!.Data);
        Assert.Equal("Grocery run", body.Data[0].Title);
    }

    #endregion

    #region Bug documentation + update validation

    [Fact]
    public async Task UpdateExpense_EmptyBody_PreservesCategoryAndPaymentMode()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId, categoryId: 1, paymentModeId: 1);

        var response = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", new { }, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal(1, body!.Data!.CategoryId);
        Assert.Equal(1, body.Data.PaymentModeId);
    }

    [Fact]
    public async Task UpdateExpense_InvalidExpenseDate_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await CreateAuthenticatedClientAsync();

        var group = await client.CreateGroupAsync();
        var adminId = group.CreatedByUserId;
        var expense = await client.CreateExpenseAsync(group.Id, adminId);

        var response = await client.PutAsJsonAsync($"/api/v1/groups/{group.Id}/expenses/{expense.Id}", new
        {
            expenseDate = "not-a-date",
            categoryId = 1,
            paymentModeId = 1,
        }, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<ExpenseDto>>(ct);
        Assert.Equal("Invalid expense date format", body!.Error!.Message);
    }

    #endregion
}