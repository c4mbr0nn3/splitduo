using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Services;
using Xunit;

namespace SplitDuo.Tests.Unit;

public class DataSeederServiceTests : IDisposable
{
    private const string WeakPassword = "changeme123";
    private const string StrongPassword = "Xk9!mQ2vLp7wZ3nR8t";

    private readonly AppOptions _appOptions = new()
    {
        Environment = Environments.Production,
        BaseUrl = "http://localhost",
        InitialUserEmail = "admin@splitduo.local",
        InitialUserFirstName = "Super",
        InitialUserLastName = "Admin",
        InitialUserPassword = WeakPassword,
        SeedDemoData = false
    };

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public DataSeederServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IOptions<AppOptions>>(Options.Create(_appOptions));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task StartAsync_ProductionWeakPasswordWithEmptyUsers_ThrowsAndSeedsNothing()
    {
        var seeder = CreateSeeder(Environments.Production);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.StartAsync(CancellationToken.None));

        Assert.Contains("SD_INITIAL_USER_PASSWORD", exception.Message);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await context.Users.CountAsync());
    }

    [Fact]
    public async Task StartAsync_ProductionStrongPassword_SeedsAdminAndVerifiesPassword()
    {
        _appOptions.InitialUserPassword = StrongPassword;
        var seeder = CreateSeeder(Environments.Production);

        await seeder.StartAsync(CancellationToken.None);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.SingleAsync();

        Assert.Equal(_appOptions.InitialUserEmail, user.Email);
        Assert.Equal(GlobalRole.SystemAdmin, user.GlobalRole);

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        Assert.Equal(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.PasswordHash, StrongPassword));
        Assert.NotEqual(PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(user, user.PasswordHash, WeakPassword));
    }

    [Fact]
    public async Task StartAsync_ProductionWeakPasswordWithExistingUser_DoesNotThrow()
    {
        await using (var seedScope = _provider.CreateAsyncScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(new User
            {
                Id = 1,
                Email = "existing@splitduo.local",
                PasswordHash = "hash",
                FirstName = "Existing",
                LastName = "User"
            });
            await context.SaveChangesAsync();
        }

        var seeder = CreateSeeder(Environments.Production);

        await seeder.StartAsync(CancellationToken.None);

        using var scope = _provider.CreateScope();
        var verifyContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verifyContext.Users.CountAsync());
    }

    private DataSeederService CreateSeeder(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        return new DataSeederService(
            _provider,
            NullLogger<DataSeederService>.Instance,
            TimeProvider.System,
            environment);
    }
}
