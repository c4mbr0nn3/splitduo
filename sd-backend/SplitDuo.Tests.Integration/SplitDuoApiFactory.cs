using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Respawn;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Domain.Enums;
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using SplitDuo.Core.Persistence.Interceptors;
using System.Threading.RateLimiting;
using Testcontainers.PostgreSql;

namespace SplitDuo.Tests.Integration;

public class SplitDuoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("splitduo_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString => _dbContainer.GetConnectionString();

    public SplitDuoApiFactory()
    {
        // Start the container synchronously in the constructor so it's ready
        // before WebApplicationFactory builds the host (which runs Program.cs's
        // MigrateAsync().Wait()).
        _dbContainer.StartAsync().GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development env disables: Serilog PostgreSQL sink, HTTPS redirection
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Ensure Jwt section has values even if env vars aren't set
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // --- B1: Override AppDbContext to point at the Testcontainers DB ---
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
                options.AddInterceptors(
                    sp.GetRequiredService<SoftDeleteSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });

            // Override DatabaseOptions so any service reading it directly sees the container
            services.Configure<DatabaseOptions>(opts =>
            {
                var cs = _dbContainer.GetConnectionString();
                // Npgsql conn string format: Host=...;Port=...;Database=...;Username=...;Password=...
                var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
                opts.Host = parts.GetValueOrDefault("Host", "localhost");
                opts.Port = parts.GetValueOrDefault("Port", "5432");
                opts.Database = parts.GetValueOrDefault("Database", "splitduo_test");
                opts.Username = parts.GetValueOrDefault("Username", "test");
                opts.Password = parts.GetValueOrDefault("Password", "test");
            });

            // --- C1: Replace rate limiter policies with no-ops ---
            // Remove the app's IConfigureOptions<RateLimiterOptions> (registered by AddRateLimiter),
            // then re-add permissive policies with the same names so [EnableRateLimiting("auth")]
            // on AuthController.login doesn't throw 500 when the policy is missing.
            var rlConfigDescriptors = services
                .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var d in rlConfigDescriptors) services.Remove(d);

            services.Configure<RateLimiterOptions>(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // Permissive policies (same names as the app's) so [EnableRateLimiting("auth")]
                // on AuthController.login doesn't throw 500 when the policy is missing.
                options.AddPolicy("auth", _ => RateLimitPartition.GetFixedWindowLimiter("test-bypass", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromDays(365),
                    QueueLimit = 0,
                }));
                options.AddPolicy("receipt-scan", _ => RateLimitPartition.GetFixedWindowLimiter("test-bypass", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = int.MaxValue,
                    Window = TimeSpan.FromDays(365),
                    QueueLimit = 0,
                }));
            });

            // --- C2: Remove Quartz hosted service to avoid background job side effects ---
            var quartzHosted = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType?.FullName?.Contains("Quartz") == true)
                .ToList();
            foreach (var d in quartzHosted) services.Remove(d);

            // --- C3: Remove the DataSeeder hosted service — we seed explicitly in InitializeAsync ---
            var seeder = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType?.Name == "DataSeederService")
                .ToList();
            foreach (var d in seeder) services.Remove(d);
        });
    }

    public async ValueTask InitializeAsync()
    {
        // Apply real EF migrations (Program.cs also runs MigrateAsync, but it runs against
        // the overridden AppDbContext — same container — so this is idempotent)
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        // Seed the admin user explicitly (seeder hosted service was removed)
        await SeedAdminUserAsync();

        // Build Respawner for fast per-test resets
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new[] { new Respawn.Graph.Table("__EFMigrationsHistory") },
            WithReseed = true,
        });
    }

    private async Task SeedAdminUserAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        if (await db.Users.AnyAsync()) return;

        // Match DataSeederService logic: SystemAdmin with a hashed password
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Guid = Guid.CreateVersion7(),
            Email = appOptions.InitialUserEmail,
            FirstName = appOptions.InitialUserFirstName,
            LastName = appOptions.InitialUserLastName,
            PasswordHash = passwordHasher.HashPassword(null!, appOptions.InitialUserPassword),
            GlobalRoleId = (int)GlobalRole.SystemAdmin,
            SecurityStamp = Guid.CreateVersion7().ToString(),
            Settings = new UserSettings(), // defaults: theme=auto, uiLanguage=en
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        // Respawn wipes all rows including the admin — re-seed for the next test
        await SeedAdminUserAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}
