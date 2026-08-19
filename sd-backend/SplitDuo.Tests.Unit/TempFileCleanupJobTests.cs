using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class TempFileCleanupJobTests
{
    private static readonly FakeTimeProvider TestTimeProvider =
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return context;
    }

    private static void SeedUserAndGroup(AppDbContext context)
    {
        context.Users.Add(new User
        {
            Id = 1,
            Email = "test@splitduo.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        });

        context.Groups.Add(new Group
        {
            Id = 1,
            Guid = Guid.NewGuid(),
            Name = "Test Group",
            CreatedBy = 1
        });

        context.SaveChanges();
    }

    private static Import CreateImport(int id, ImportStatus status, long updatedAt)
    {
        return new Import
        {
            Id = id,
            Guid = Guid.NewGuid(),
            GroupId = 1,
            UserId = 1,
            FileName = "test.csv",
            FileHash = $"test-hash-{id}",
            ImportDate = new DateOnly(2026, 1, 1),
            Status = status,
            ImportType = ImportType.SplitDuo,
            TempFile = [1, 2, 3],
            UpdatedAt = updatedAt
        };
    }

    private static TempFileCleanupJob CreateJob(AppDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);

        return new TempFileCleanupJob(
            NullLogger<TempFileCleanupJob>.Instance,
            unitOfWork,
            TestTimeProvider);
    }

    [Fact]
    public async Task Execute_ClearsCompletedAndFailedTempFiles_OlderThanOneDay()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedUserAndGroup(context);

        var now = TestTimeProvider.GetUtcNow();
        var oldCompleted = CreateImport(1, ImportStatus.Completed, now.AddDays(-2).ToUnixTimeSeconds());
        var recentCompleted = CreateImport(2, ImportStatus.Completed, now.AddHours(-12).ToUnixTimeSeconds());
        var oldFailed = CreateImport(3, ImportStatus.Failed, now.AddDays(-2).ToUnixTimeSeconds());

        context.Imports.AddRange(oldCompleted, recentCompleted, oldFailed);
        context.SaveChanges();

        var job = CreateJob(context);
        await job.Execute(Substitute.For<IJobExecutionContext>());

        Assert.Null(oldCompleted.TempFile);
        Assert.Null(oldFailed.TempFile);
        Assert.NotNull(recentCompleted.TempFile);
    }

    [Fact]
    public async Task Execute_ClearsPendingAndProcessingTempFiles_OlderThanSevenDays()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedUserAndGroup(context);

        var now = TestTimeProvider.GetUtcNow();
        var oldPending = CreateImport(1, ImportStatus.Pending, now.AddDays(-8).ToUnixTimeSeconds());
        var oldProcessing = CreateImport(2, ImportStatus.Processing, now.AddDays(-8).ToUnixTimeSeconds());
        var recentPending = CreateImport(3, ImportStatus.Pending, now.AddDays(-3).ToUnixTimeSeconds());

        context.Imports.AddRange(oldPending, oldProcessing, recentPending);
        context.SaveChanges();

        var job = CreateJob(context);
        await job.Execute(Substitute.For<IJobExecutionContext>());

        Assert.Null(oldPending.TempFile);
        Assert.Null(oldProcessing.TempFile);
        Assert.NotNull(recentPending.TempFile);
    }

    [Fact]
    public async Task Execute_LeavesRecentTempFiles_Untouched()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);
        SeedUserAndGroup(context);

        var now = TestTimeProvider.GetUtcNow();
        var recentCompleted = CreateImport(1, ImportStatus.Completed, now.AddHours(-12).ToUnixTimeSeconds());
        var recentFailed = CreateImport(2, ImportStatus.Failed, now.AddHours(-12).ToUnixTimeSeconds());
        var recentPending = CreateImport(3, ImportStatus.Pending, now.AddDays(-3).ToUnixTimeSeconds());
        var recentProcessing = CreateImport(4, ImportStatus.Processing, now.AddDays(-3).ToUnixTimeSeconds());

        context.Imports.AddRange(recentCompleted, recentFailed, recentPending, recentProcessing);
        context.SaveChanges();

        var job = CreateJob(context);
        await job.Execute(Substitute.For<IJobExecutionContext>());

        Assert.NotNull(recentCompleted.TempFile);
        Assert.NotNull(recentFailed.TempFile);
        Assert.NotNull(recentPending.TempFile);
        Assert.NotNull(recentProcessing.TempFile);
    }

    [Fact]
    public async Task Execute_Throws_LogsAndRethrows()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var context = CreateContext(connection);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Imports.Returns(context.Imports);
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Save failed"));

        var job = new TempFileCleanupJob(
            NullLogger<TempFileCleanupJob>.Instance,
            unitOfWork,
            TestTimeProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.Execute(Substitute.For<IJobExecutionContext>()));
    }
}
