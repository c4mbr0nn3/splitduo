using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using NSubstitute;
using SplitDuo.Api.Features.Expenses.Dto;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.Expenses;
using Xunit;

namespace SplitDuo.Tests.Unit;

/// <summary>
/// Unit tests for the GetGroupExpenses "Paid by" filter (issue: alias GUIDs were
/// sent in the userId param and silently skipped by the backend).
///
/// Contract: a first-class aliasId filter is applied as e.PaidByAliasId == alias.Id
/// and validated against the group; an unresolved userId now returns BadRequest
/// instead of being silently ignored.
/// </summary>
public class ExpensesServiceGetGroupExpensesFilterTests
{
    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }

    private static IStringLocalizer<ExpensesService> CreateLocalizer()
    {
        var loc = Substitute.For<IStringLocalizer<ExpensesService>>();
        // NSubstitute's default for LocalizedString-valued indexer calls is null,
        // which breaks string.Format when a message is interpolated — stub the keys
        // used by the filter paths under test.
        loc["AliasNotInGroup"].Returns(new LocalizedString(
            "AliasNotInGroup", "Alias {0} does not belong to this group"));
        loc["AliasNotFound"].Returns(new LocalizedString("AliasNotFound", "Alias not found"));
        loc["InvalidAliasIdFormat"].Returns(new LocalizedString("InvalidAliasIdFormat", "Invalid alias ID format"));
        loc["InvalidPaidByUserIdFormat"].Returns(new LocalizedString("InvalidPaidByUserIdFormat", "Invalid paid by user ID format"));
        loc["PaidByUserNotFound"].Returns(new LocalizedString("PaidByUserNotFound", "Paid by user not found"));
        return loc;
    }

    private static ExpensesService CreateService(AppDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);
        var creationService = new ExpenseCreationService(
            unitOfWork, Substitute.For<IStringLocalizer<ExpenseCreationService>>());

        return new(unitOfWork, Substitute.For<TimeProvider>(), creationService, CreateLocalizer());
    }

    private static (AppDbContext Context, Guid GroupGuid, Guid User1Guid, Guid AliasAGuid, Guid AliasBGuid) Seed(AppDbContext context)
    {
        context.Users.Add(new User
        {
            Id = 1, Guid = Guid.NewGuid(),
            Email = "admin@splitduo.local", PasswordHash = "hash",
            FirstName = "Admin", LastName = "Test"
        });
        context.Users.Add(new User
        {
            Id = 2, Guid = Guid.NewGuid(),
            Email = "other@splitduo.local", PasswordHash = "hash",
            FirstName = "Other", LastName = "Test"
        });

        context.Groups.Add(new Group
        {
            Id = 1, Guid = Guid.NewGuid(), Name = "Alias Group",
            CreatedBy = 1, UseAliases = true, AliasSetupFinalized = true
        });
        context.Groups.Add(new Group
        {
            Id = 2, Guid = Guid.NewGuid(), Name = "Other Group",
            CreatedBy = 2, UseAliases = true, AliasSetupFinalized = true
        });

        context.Aliases.Add(new Alias { Id = 1, GroupId = 1, Name = "AliasA", IsSingleton = true });
        context.Aliases.Add(new Alias { Id = 2, GroupId = 1, Name = "AliasB", IsSingleton = true });
        // Alias in a different group — for the AliasNotInGroup authz test.
        context.Aliases.Add(new Alias { Id = 3, GroupId = 2, Name = "ForeignAlias", IsSingleton = true });

        context.GroupMembers.Add(new GroupMember { Id = 1, GroupId = 1, UserId = 1, RoleId = 1, AliasId = 1 });
        context.GroupMembers.Add(new GroupMember { Id = 2, GroupId = 1, UserId = 2, RoleId = 1, AliasId = 2 });

        context.Expenses.Add(new Expense
        {
            Id = 1, GroupId = 1, Title = "Paid by alias A", Amount = 50m,
            PaidBy = 1, PaidByAliasId = 1,
            ExpenseDate = new DateOnly(2026, 1, 1), CategoryId = 1, PaymentModeId = 1
        });
        context.Expenses.Add(new Expense
        {
            Id = 2, GroupId = 1, Title = "Paid by alias B", Amount = 30m,
            PaidBy = 2, PaidByAliasId = 2,
            ExpenseDate = new DateOnly(2026, 1, 2), CategoryId = 1, PaymentModeId = 1
        });
        // Soft-deleted alias (no DeletedAt predicate in filter) with a historical split.
        context.Aliases.Add(new Alias
        {
            Id = 4, GroupId = 1, Name = "AliasDeleted", IsSingleton = true,
            DeletedAt = new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()
        });
        context.Expenses.Add(new Expense
        {
            Id = 3, GroupId = 1, Title = "Paid by deleted alias", Amount = 20m,
            PaidBy = 1, PaidByAliasId = 4,
            ExpenseDate = new DateOnly(2026, 1, 3), CategoryId = 1, PaymentModeId = 1
        });

        context.SaveChanges();

        return (
            context,
            context.Groups.First().Guid,
            context.Users.First(u => u.Id == 1).Guid,
            context.Aliases.First(a => a.Id == 1).Guid,
            context.Aliases.First(a => a.Id == 2).Guid
        );
    }

    [Fact]
    public async Task GetGroupExpenses_AliasIdFilter_ReturnsOnlyMatchingExpenses()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(AliasId: seeded.AliasAGuid.ToString()));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Data);
        var expense = Assert.Single(result.Value.Data);
        Assert.Equal("Paid by alias A", expense.Title);
    }

    [Fact]
    public async Task GetGroupExpenses_UserIdFilter_MatchingUser_ReturnsOnlyMatchingExpenses()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(UserId: user1Guid.ToString()));

        Assert.True(result.IsSuccess);
        // Deterministic: the two seeded expenses paid by user 1 (via alias A and the
        // soft-deleted alias). Fails if the PaidBy predicate is removed or weakened.
        Assert.Equal(2, result.Value!.Data.Count());
        Assert.All(result.Value.Data, e => Assert.Contains("alias", e.Title));

        var user2Guid = context.Users.First(u => u.Id == 2).Guid;
        var result2 = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(UserId: user2Guid.ToString()));

        Assert.True(result2.IsSuccess);
        var onlyExpense = Assert.Single(result2.Value!.Data);
        Assert.Equal("Paid by alias B", onlyExpense.Title);
    }

    [Fact]
    public async Task GetGroupExpenses_AliasIdFilter_SoftDeletedAliasWithHistory_ReturnsItsExpenses()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;
        var deletedAliasGuid = context.Aliases.First(a => a.Id == 4).Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(AliasId: deletedAliasGuid.ToString()));

        Assert.True(result.IsSuccess);
        var expense = Assert.Single(result.Value!.Data);
        Assert.Equal("Paid by deleted alias", expense.Title);
    }

    [Fact]
    public async Task GetGroupExpenses_AliasIdFilter_ForeignGroupAlias_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First(g => g.Id == 1).Guid;
        var foreignAliasGuid = context.Aliases.First(a => a.Id == 3).Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(AliasId: foreignAliasGuid.ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task GetGroupExpenses_UnknownUserId_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(UserId: Guid.NewGuid().ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task GetGroupExpenses_UnknownAliasId_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(AliasId: Guid.NewGuid().ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task GetGroupExpenses_InvalidAliasIdFormat_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        var seeded = Seed(context);

        var service = CreateService(context);
        var user1Guid = context.Users.First(u => u.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var result = await service.GetGroupExpensesAsync(
            groupGuid.ToString(), user1Guid, 1, 20,
            new ExpenseFilterOptions(AliasId: "not-a-guid"));

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}