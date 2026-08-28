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
using Xunit;

namespace SplitDuo.Tests.Unit;

/// <summary>
/// Unit tests for the issue #31 fix: in alias-mode groups, the payer must have an
/// assigned alias (GroupMember.AliasId) before creating an expense. Without this
/// guard, PaidByAliasId would be null and the expense would be silently excluded
/// from alias balance calculations.
///
/// The API layer cannot produce an "unassigned member" (AddGroupMemberAsync and
/// alias management always assign a singleton alias), so this scenario is tested
/// directly against the service with a seeded database where the payer's
/// GroupMember has AliasId = null.
/// </summary>
public class ExpensesServicePayerAliasTests
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
        loc["PayerMissingAlias"].Returns(new LocalizedString(
            "PayerMissingAlias",
            "The payer must be assigned to a subgroup before creating expenses in this group."));
        return loc;
    }

    private static ExpensesService CreateService(AppDbContext context) =>
        new(new UnitOfWork(context), Substitute.For<TimeProvider>(), CreateLocalizer());

    [Fact]
    public async Task CreateExpense_AliasMode_PayerWithoutAlias_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        context.Users.Add(new User
        {
            Id = 1, Guid = Guid.NewGuid(),
            Email = "admin@splitduo.local", PasswordHash = "hash",
            FirstName = "Admin", LastName = "Test"
        });
        context.Users.Add(new User
        {
            Id = 2, Guid = Guid.NewGuid(),
            Email = "payer@splitduo.local", PasswordHash = "hash",
            FirstName = "Payer", LastName = "Test"
        });

        context.Groups.Add(new Group
        {
            Id = 1, Guid = Guid.NewGuid(), Name = "Alias Group",
            CreatedBy = 1, UseAliases = true, AliasSetupFinalized = true
        });

        // Payer's membership has NO alias (AliasId = null) — the issue #31 scenario.
        context.GroupMembers.Add(new GroupMember { Id = 1, GroupId = 1, UserId = 1, RoleId = 1, AliasId = 1 });
        context.GroupMembers.Add(new GroupMember { Id = 2, GroupId = 1, UserId = 2, RoleId = 1, AliasId = null });

        context.Aliases.Add(new Alias { Id = 1, GroupId = 1, Name = "Alias1", IsSingleton = true });

        context.SaveChanges();

        var service = CreateService(context);
        var payerGuid = context.Users.First(u => u.Id == 2).Guid;
        var alias1Guid = context.Aliases.First(a => a.Id == 1).Guid;
        var groupGuid = context.Groups.First().Guid;

        var request = new CreateExpenseRequestDto
        {
            Title = "Test",
            Amount = 100m,
            PaidByUserId = payerGuid.ToString(),
            ExpenseDate = "2026-01-15",
            CategoryId = 1,
            PaymentModeId = 1,
            AliasSplits = [new CreateExpenseAliasSplitDto { AliasId = alias1Guid.ToString(), SplitAmount = 100m }]
        };

        // Pass the payer's own Guid as currentUserId so the auth check passes
        // and the code reaches the alias validation guard.
        var result = await service.CreateExpenseAsync(groupGuid.ToString(), payerGuid, request);

        Assert.True(result.IsFailure);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task CreateExpense_AliasMode_PayerWithAlias_Succeeds()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        context.Users.Add(new User
        {
            Id = 1, Guid = Guid.NewGuid(),
            Email = "admin@splitduo.local", PasswordHash = "hash",
            FirstName = "Admin", LastName = "Test"
        });
        context.Users.Add(new User
        {
            Id = 2, Guid = Guid.NewGuid(),
            Email = "payer@splitduo.local", PasswordHash = "hash",
            FirstName = "Payer", LastName = "Test"
        });

        context.Groups.Add(new Group
        {
            Id = 1, Guid = Guid.NewGuid(), Name = "Alias Group",
            CreatedBy = 1, UseAliases = true, AliasSetupFinalized = true
        });

        context.GroupMembers.Add(new GroupMember { Id = 1, GroupId = 1, UserId = 1, RoleId = 1, AliasId = 1 });
        context.GroupMembers.Add(new GroupMember { Id = 2, GroupId = 1, UserId = 2, RoleId = 1, AliasId = 2 });

        context.Aliases.Add(new Alias { Id = 1, GroupId = 1, Name = "Alias1", IsSingleton = true });
        context.Aliases.Add(new Alias { Id = 2, GroupId = 1, Name = "Alias2", IsSingleton = true });

        context.SaveChanges();

        var service = CreateService(context);
        var payerGuid = context.Users.First(u => u.Id == 2).Guid;
        var alias2Guid = context.Aliases.First(a => a.Id == 2).Guid;
        var groupGuid = context.Groups.First().Guid;

        var request = new CreateExpenseRequestDto
        {
            Title = "Test",
            Amount = 100m,
            PaidByUserId = payerGuid.ToString(),
            ExpenseDate = "2026-01-15",
            CategoryId = 1,
            PaymentModeId = 1,
            AliasSplits = [new CreateExpenseAliasSplitDto { AliasId = alias2Guid.ToString(), SplitAmount = 100m }]
        };

        // Payer has an alias, so the guard passes and the expense is created.
        var result = await service.CreateExpenseAsync(groupGuid.ToString(), payerGuid, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }
}