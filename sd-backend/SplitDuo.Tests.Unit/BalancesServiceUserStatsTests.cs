using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Localization;
using NSubstitute;
using SplitDuo.Api.Features.Expenses.Services;
using SplitDuo.Api.Features.Users.Dto;
using SplitDuo.Core.Common;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class BalancesServiceUserStatsTests
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

    private static IStringLocalizer<BalancesService> CreateLocalizer()
    {
        var loc = Substitute.For<IStringLocalizer<BalancesService>>();
        loc["UserNotFound"].Returns(new LocalizedString("UserNotFound", "User not found"));
        return loc;
    }

    private static User SeedUser(AppDbContext context, int id, Guid guid)
    {
        var user = new User
        {
            Id = id,
            Guid = guid,
            Email = $"user{id}@splitduo.local",
            PasswordHash = "hash",
            FirstName = $"User{id}",
            LastName = "Test"
        };
        context.Users.Add(user);
        return user;
    }

    #region UserWithNoGroups_ReturnsZeros

    [Fact]
    public async Task UserWithNoGroups_ReturnsZeros()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedUser(context, 1, Guid.NewGuid());
        context.SaveChanges();

        var service = new BalancesService(
            new UnitOfWork(context),
            Substitute.For<HybridCache>(),
            CreateLocalizer());
        var userGuid = context.Users.First().Guid;

        var result = await service.GetCurrentUserStatsAsync(userGuid);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalGroups);
        Assert.Equal(0, result.Value.Individual.Groups);
        Assert.Equal(0m, result.Value.Individual.YouOwe);
        Assert.Equal(0m, result.Value.Individual.YoureOwed);
        Assert.Equal(0, result.Value.Alias.Groups);
        Assert.Equal(0m, result.Value.Alias.YouOwe);
        Assert.Equal(0m, result.Value.Alias.YoureOwed);
        Assert.Equal(result.Value.TotalGroups, result.Value.Individual.Groups + result.Value.Alias.Groups);
    }

    #endregion

    #region IndividualMode_ExpenseSplit_ReturnsOwedAndOwe

    [Fact]
    public async Task IndividualMode_ExpenseSplit_ReturnsOwedAndOwe()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedUser(context, 1, Guid.NewGuid());
        SeedUser(context, 2, Guid.NewGuid());

        context.Groups.Add(new Group
        {
            Id = 1,
            Guid = Guid.NewGuid(),
            Name = "Group",
            CreatedBy = 1,
            UseAliases = false,
            AliasSetupFinalized = false
        });

        context.GroupMembers.Add(new GroupMember { Id = 1, GroupId = 1, UserId = 1, RoleId = 1 });
        context.GroupMembers.Add(new GroupMember { Id = 2, GroupId = 1, UserId = 2, RoleId = 1 });

        context.Expenses.Add(new Expense
        {
            Id = 1,
            GroupId = 1,
            Title = "Dinner",
            Amount = 100m,
            PaidBy = 1,
            ExpenseDate = new DateOnly(2026, 1, 1),
            CategoryId = 1,
            PaymentModeId = 1,
            PaidByAliasId = null,
            DeletedAt = null
        });

        context.ExpenseSplits.Add(new ExpenseSplit { Id = 1, ExpenseId = 1, UserId = 1, SplitAmount = 50m });
        context.ExpenseSplits.Add(new ExpenseSplit { Id = 2, ExpenseId = 1, UserId = 2, SplitAmount = 50m });

        context.SaveChanges();

        var service = new BalancesService(
            new UnitOfWork(context),
            Substitute.For<HybridCache>(),
            CreateLocalizer());
        var userGuid = context.Users.First().Guid;

        var result = await service.GetCurrentUserStatsAsync(userGuid);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalGroups);
        Assert.Equal(100m, result.Value.Individual.YoureOwed);
        Assert.Equal(50m, result.Value.Individual.YouOwe);
        Assert.Equal(0, result.Value.Alias.Groups);
        Assert.Equal(0m, result.Value.Alias.YouOwe);
        Assert.Equal(0m, result.Value.Alias.YoureOwed);
        Assert.Equal(result.Value.TotalGroups, result.Value.Individual.Groups + result.Value.Alias.Groups);
    }

    #endregion

    #region AliasMode_ExpenseWithPaidByAliasId_AttributesToPayerAlias

    [Fact]
    public async Task AliasMode_ExpenseWithPaidByAliasId_AttributesToPayerAlias()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedAliasGroup(context, expensePaidByAliasId: 1);

        var service = new BalancesService(
            new UnitOfWork(context),
            Substitute.For<HybridCache>(),
            CreateLocalizer());
        var userGuid = context.Users.First().Guid;

        var result = await service.GetCurrentUserStatsAsync(userGuid);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalGroups);
        Assert.Equal(1, result.Value.Alias.Groups);
        Assert.Equal(100m, result.Value.Alias.YoureOwed);
        Assert.Equal(50m, result.Value.Alias.YouOwe);
        Assert.Equal(0, result.Value.Individual.Groups);
        Assert.Equal(0m, result.Value.Individual.YouOwe);
        Assert.Equal(0m, result.Value.Individual.YoureOwed);
        Assert.Equal(result.Value.TotalGroups, result.Value.Individual.Groups + result.Value.Alias.Groups);
    }

    #endregion

    #region AliasMode_ExpenseWithNullPaidByAliasId_FallsBackToCurrentAlias

    [Fact]
    public async Task AliasMode_ExpenseWithNullPaidByAliasId_FallsBackToCurrentAlias()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedAliasGroup(context, expensePaidByAliasId: null);

        var service = new BalancesService(
            new UnitOfWork(context),
            Substitute.For<HybridCache>(),
            CreateLocalizer());
        var userGuid = context.Users.First().Guid;

        var result = await service.GetCurrentUserStatsAsync(userGuid);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalGroups);
        Assert.Equal(1, result.Value.Alias.Groups);
        Assert.Equal(100m, result.Value.Alias.YoureOwed);
        Assert.Equal(50m, result.Value.Alias.YouOwe);
        Assert.Equal(0, result.Value.Individual.Groups);
        Assert.Equal(0m, result.Value.Individual.YouOwe);
        Assert.Equal(0m, result.Value.Individual.YoureOwed);
        Assert.Equal(result.Value.TotalGroups, result.Value.Individual.Groups + result.Value.Alias.Groups);
    }

    #endregion

    #region NonexistentUser_ReturnsNotFound

    [Fact]
    public async Task NonexistentUser_ReturnsNotFound()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        context.SaveChanges();

        var service = new BalancesService(
            new UnitOfWork(context),
            Substitute.For<HybridCache>(),
            CreateLocalizer());

        var result = await service.GetCurrentUserStatsAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    #endregion

    private static void SeedAliasGroup(AppDbContext context, int? expensePaidByAliasId)
    {
        SeedUser(context, 1, Guid.NewGuid());
        SeedUser(context, 2, Guid.NewGuid());

        context.Groups.Add(new Group
        {
            Id = 1,
            Guid = Guid.NewGuid(),
            Name = "Alias Group",
            CreatedBy = 1,
            UseAliases = true,
            AliasSetupFinalized = true
        });

        context.Aliases.Add(new Alias { Id = 1, GroupId = 1, Name = "Alias1", IsSingleton = null });
        context.Aliases.Add(new Alias { Id = 2, GroupId = 1, Name = "Alias2", IsSingleton = true });

        context.GroupMembers.Add(new GroupMember { Id = 1, GroupId = 1, UserId = 1, RoleId = 1, AliasId = 1 });
        context.GroupMembers.Add(new GroupMember { Id = 2, GroupId = 1, UserId = 2, RoleId = 1, AliasId = 2 });

        context.Expenses.Add(new Expense
        {
            Id = 1,
            GroupId = 1,
            Title = "Groceries",
            Amount = 100m,
            PaidBy = 1,
            ExpenseDate = new DateOnly(2026, 1, 1),
            CategoryId = 1,
            PaymentModeId = 1,
            PaidByAliasId = expensePaidByAliasId,
            DeletedAt = null
        });

        context.ExpenseAliasSplits.Add(new ExpenseAliasSplit { Id = 1, ExpenseId = 1, AliasId = 1, SplitAmount = 50m });
        context.ExpenseAliasSplits.Add(new ExpenseAliasSplit { Id = 2, ExpenseId = 1, AliasId = 2, SplitAmount = 50m });

        context.SaveChanges();
    }
}