using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services.BackgroundJobs;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class RefreshTokenCleanupJobTests
{
    private static readonly FakeTimeProvider TestTimeProvider =
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    // SQLite in-memory (not EF InMemory): ExecuteDeleteAsync is a relational-only feature
    // and is not supported by the InMemory provider. The connection must stay open for the
    // lifetime of the context, otherwise the in-memory database is dropped.
    private static (AppDbContext Context, SqliteConnection Connection) CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }

    private static void SeedUser(AppDbContext context)
    {
        context.Users.Add(new User
        {
            Id = 1,
            Email = "test@splitduo.local",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        });

        context.SaveChanges();
    }

    private static RefreshToken CreateRefreshToken(int id, long expiresAt)
    {
        return new RefreshToken
        {
            Id = id,
            UserId = 1,
            TokenHash = $"token-hash-{id}",
            JwtId = $"jwt-{id}",
            FamilyId = $"family-{id}",
            ExpiresAt = expiresAt,
            ClientInfo = "test-client"
        };
    }

    private static RefreshTokenCleanupJob CreateJob(AppDbContext context)
    {
        var unitOfWork = new UnitOfWork(context);

        return new RefreshTokenCleanupJob(
            NullLogger<RefreshTokenCleanupJob>.Instance,
            unitOfWork,
            TestTimeProvider);
    }

    private static IJobExecutionContext CreateJobExecutionContext()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Execute_DeletesExpiredTokens()
    {
        var (context, connection) = CreateContext();
        await using var ctx = context;
        using var conn = connection;

        SeedUser(context);

        var now = TestTimeProvider.GetUtcNow();
        var expiredToken1 = CreateRefreshToken(1, now.AddDays(-10).ToUnixTimeSeconds());
        var expiredToken2 = CreateRefreshToken(2, now.AddDays(-8).ToUnixTimeSeconds());
        var validToken = CreateRefreshToken(3, now.AddDays(-3).ToUnixTimeSeconds());

        context.RefreshTokens.AddRange(expiredToken1, expiredToken2, validToken);
        context.SaveChanges();

        var job = CreateJob(context);
        await job.Execute(CreateJobExecutionContext());

        Assert.Equal(1, context.RefreshTokens.Count());
        Assert.Equal(3, context.RefreshTokens.Single().Id);
    }

    [Fact]
    public async Task Execute_NoExpiredTokens_DeletesZero()
    {
        var (context, connection) = CreateContext();
        await using var ctx = context;
        using var conn = connection;

        SeedUser(context);

        var now = TestTimeProvider.GetUtcNow();
        var validToken1 = CreateRefreshToken(1, now.AddDays(-3).ToUnixTimeSeconds());
        var validToken2 = CreateRefreshToken(2, now.AddDays(-3).ToUnixTimeSeconds());

        context.RefreshTokens.AddRange(validToken1, validToken2);
        context.SaveChanges();

        var job = CreateJob(context);
        await job.Execute(CreateJobExecutionContext());

        Assert.Equal(2, context.RefreshTokens.Count());
    }
}
