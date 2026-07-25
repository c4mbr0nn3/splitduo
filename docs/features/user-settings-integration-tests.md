# Integration Tests Plan — User Settings Feature

| Field | Value |
|---|---|
| Status | Plan — ready for implementation |
| Version | 1.0 |
| Date | 2026-07-25 |
| Owner | Engineering |
| Scope | Bootstrap integration testing in `SplitDuo.Tests.Integration`; first tests target the user-settings feature (`PUT /api/v1/users/me/settings`, `GET /api/v1/users/me`) |
| Runtime | Podman (rootless) — no Docker on this system |

## Executive Summary

The repo has two skeleton test projects (`SplitDuo.Tests.Unit`, `SplitDuo.Tests.Integration`) with **zero test files** — only `.csproj` + build artifacts. This plan bootstraps integration testing by standing up a real PostgreSQL 17 container via Testcontainers for .NET (configured for podman), running the full ASP.NET Core pipeline through `WebApplicationFactory<Program>`, applying the app's real EF migrations, seeding the admin user, and writing the first integration tests against the user-settings endpoints.

Four concrete blockers must be handled in the test host: (1) `Program.cs` runs `Database.MigrateAsync().Wait()` unconditionally — must point at the Testcontainers DB; (2) `UseHttpsRedirection()` issues 307 redirects on the HTTP test client — must be neutralized; (3) `Jwt.SecretKey`/`Issuer`/`Audience` are empty in `appsettings.json` — must be supplied via env vars or config override; (4) the rate limiter on `/auth/login` (10 req/min) could throttle test logins — must be replaced with a no-op limiter. The seeder and Quartz jobs are left running (seeder is desirable — it creates the admin user; Quartz jobs are low-risk on short test runs).

## Goals & Non-Goals

### Goals
- Stand up a PostgreSQL 17 container via Testcontainers + podman, shared across the test run.
- Run the full SplitDuo API in-process via `WebApplicationFactory<Program>` against the container DB.
- Apply real EF migrations (12, including `AddUserSettings`) to the container DB.
- Seed the admin user (`admin@localhost` / `changeme`) so tests can authenticate.
- Write the first integration tests for the user-settings feature: round-trip persistence, validation, auth enforcement, defaults on new user, cross-request persistence.
- Provide a reusable test harness (`SplitDuoApiFactory` + `IntegrationTest` base class) for future tests.
- Provide a runnable script that configures podman env vars and launches `dotnet test`.

### Non-Goals
- Unit tests (`SplitDuo.Tests.Unit`) — separate effort.
- Frontend tests — out of scope; this is backend integration only.
- Testcontainers for services other than PostgreSQL (no Redis, no SMTP container).
- Mocking the database — the whole point is a real PostgreSQL with jsonb.
- Testing endpoints beyond the settings feature + the auth login needed to get a token. (The harness is reusable; future tests extend it.)
- CI pipeline integration — local-first for now; CI can reuse the podman env vars.

## Background & Current State

### Existing test projects (skeletons, no tests)

`sd-backend/SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj` (23 lines):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="NSubstitute" Version="6.0.0" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.8.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.10" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SplitDuo.Api\SplitDuo.Api.csproj" />
  </ItemGroup>
</Project>
```

Already present: xUnit v3 (`xunit.v3` 3.2.2), `Microsoft.NET.Test.Sdk` 18.8.1, `Microsoft.AspNetCore.Mvc.Testing` 10.0.10, `NSubstitute` 6.0.0, `Microsoft.Extensions.TimeProvider.Testing` 10.8.0, `ProjectReference` to `SplitDuo.Api`.

Missing: `Testcontainers.PostgreSql`, `Respawn`, `Npgsql` (direct — needed for Respawn's connection).

No `Directory.Build.props` / `Directory.Packages.props` — package versions are direct per `.csproj`. No `appsettings.Test.json`. No `.runsettings`. Solution `sd-backend/sd-backend.sln` already includes both test projects.

### App startup pipeline (`Program.cs`, 20 lines)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServices();          // Api/Extensions/ApiProgramExtensions.AddServices
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.MigrateAsync().Wait();   // ⚠️ unconditional, blocking
}
app.ConfigureServices();         // middleware pipeline
app.Run();
```

Top-level statements → compiler generates a `Program` class → `WebApplicationFactory<Program>` works with a `ProjectReference` to `SplitDuo.Api`. No `Startup.cs`, no env-var branching, no `IHostEnvironment` hooks to skip migrations.

### Blockers identified (from codebase exploration)

| # | Blocker | Location | Impact | Mitigation |
|---|---|---|---|---|
| **B1** | `Database.MigrateAsync().Wait()` runs on host start | `Program.cs:15` | Will run against whatever DB is configured | Point `AppDbContext` at the Testcontainers DB via `ConfigureTestServices` — migrations then apply to the container (desirable: creates schema) |
| **B2** | `UseHttpsRedirection()` | `Api/Extensions/ApiProgramExtensions.cs:118` | 307 redirects on every HTTP test request | Set test host environment to `"Development"` (HTTPS redirect is gated on `!IsDevelopment()` per standard ASP.NET Core) OR remove the middleware via a test-only `IStartupFilter` |
| **B3** | `Jwt.SecretKey` empty in `appsettings.json` | `appsettings.json:34` | `JwtBearerOptionsSetup` throws if empty | Set `SD_JWT_SECRET_KEY` env var (or override `JwtOptions` in `ConfigureTestServices`) |
| **B4** | `Jwt.Issuer`/`Audience` empty in `appsettings.json` | `appsettings.json:35-36` | Token validation fails against empty strings | Set `SD_JWT_ISSUER`/`SD_JWT_AUDIENCE` env vars (or override) |
| **C1** | Rate limiter on `/auth/login` (10 req/min, `"auth"` policy) | `Api/Extensions/ApiProgramExtensions.cs:83-104`, `AuthController.cs:21` | Test logins throttled after 10/min | Replace `RateLimiterOptions` registration with a no-op `PartitionedRateLimiter.CreateNoLimiter` in `ConfigureTestServices` |
| **C2** | Quartz jobs fire during tests (email every 2 min, cleanup daily) | `Core/Extensions/ApiProgramExtensions.cs:94-140` | Low risk for short runs; email job needs SMTP config to actually send | Leave running; set `SmtpOptions` to dummy values or leave `SmtpServer` empty (email job will fail silently on send and retry). Optionally remove `AddQuartzHostedService` registration in `ConfigureTestServices` for cleanliness. |
| **C3** | `DataSeederService` runs on every host start | `Core/Extensions/ApiProgramExtensions.cs:89` | Creates admin user if DB empty | **Desirable** — leaves it running so tests get a seeded admin. Disable demo data via `SD_SEED_DEMO_DATA=false` (default). |
| **C4** | Serilog PostgreSQL sink in non-Development env | `Core/Extensions/ApiProgramExtensions.cs:33-43` | Writes logs to test DB (noise, not a blocker) | Set test host environment to `"Development"` (sink is Development-gated) — also fixes B2 |

### What works in our favor
- `WebApplicationFactory` package already referenced.
- xUnit v3 already set up.
- All DB env vars have defaults (`DatabaseOptionsSetup`) — overridable via `ConfigureTestServices` or env vars.
- Seeder creates a known admin (`admin@localhost` / `changeme`) — perfect for test auth.
- `GlobalExceptionHandler` returns structured `ProblemDetails` — easy to assert on error responses.
- Settings endpoint has **no** rate limiter attribute — only `/auth/login`, `/auth/verify-2fa`, `/receipts/parse` do.
- `postgres:17-alpine` matches docker-compose — Testcontainers image is settled.
- `UserSettings` is a simple jsonb complex type — no relationships to wrangle in tests.

## Architecture

### Component overview

```
┌─────────────────────────────────────────────────────────────┐
│  SplitDuo.Tests.Integration (xUnit v3)                       │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  SplitDuoApiFactory : WebApplicationFactory<Program> │  │
│  │  - PostgreSqlContainer (Testcontainers, podman)       │  │
│  │  - ConfigureTestServices:                            │  │
│  │      • override AppDbContext → container conn string  │  │
│  │      • override DatabaseOptions → container host/port │  │
│  │      • replace RateLimiterOptions → no-op             │  │
│  │      • (optional) remove Quartz hosted service        │  │
│  │  - InitializeAsync: start container, MigrateAsync,    │  │
│  │    build Respawner                                    │  │
│  │  - ResetDatabaseAsync: Respawn.ResetAsync             │  │
│  └───────────────────────────────────────────────────────┘  │
│                          ▲                                   │
│            ICollectionFixture<SplitDuoApiFactory>             │
│                          │                                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  IntegrationTest (abstract base, IAsyncLifetime)       │  │
│  │  - HttpClient (factory.CreateClient)                   │  │
│  │  - GetAuthTokenAsync (POST /auth/login)                │  │
│  │  - CreateAuthenticatedClientAsync                      │  │
│  │  - DisposeAsync: factory.ResetDatabaseAsync            │  │
│  └───────────────────────────────────────────────────────┘  │
│                          ▲                                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  UserSettingsTests : IntegrationTest                  │  │
│  │  - GetSettings_ReturnsDefaults_ForSeededAdmin          │  │
│  │  - UpdateTheme_PersistsAndReturnsUpdated              │  │
│  │  - UpdateTheme_AppliesOnSubsequentGet                 │  │
│  │  - UpdateLanguage_Persists                            │  │
│  │  - UpdateTheme_InvalidValue_Returns400                │  │
│  │  - UpdateLanguage_InvalidValue_Returns400             │  │
│  │  - UpdateSettings_Unauthenticated_Returns401          │  │
│  │  - UpdateSettings_EmptyBody_LeavesSettingsUnchanged   │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
         │
         ▼ (Testcontainers via Docker-compatible socket)
┌─────────────────────────────────────────────────────────────┐
│  Podman (rootless)                                          │
│  socket: unix:///run/user/<uid>/podman/podman.sock          │
│  Container: postgres:17-alpine                              │
│  DB: splitduo_test, user: test, password: test              │
└─────────────────────────────────────────────────────────────┘
```

### Key design decisions

1. **One container per test run** (xUnit `ICollectionFixture`), not per test class or per test. Container startup is ~2–3s; tests run in <1s total after that. Respawn resets the DB between tests in ~20–50ms.
2. **Real EF migrations** (`MigrateAsync`), not `EnsureCreatedAsync`. Matches production schema exactly, exercises the migration history, and catches migration bugs.
3. **Real login for auth** (Pattern A), not direct token generation. Tests the full auth flow and guarantees the token's claims match a real DB user. The rate limiter is disabled so repeated logins don't throttle.
4. **Respawn for cleanup**, not transaction rollback (incompatible with `WebApplicationFactory`'s per-request DI scopes) and not drop/recreate (too slow). Respawn handles jsonb columns and FKs via topological sort.
5. **Environment = "Development"** on the test host. This disables the Serilog PostgreSQL sink (B2/C4 mitigation) and the HTTPS redirect middleware (B2 mitigation) — both are gated on `!IsDevelopment()`.
6. **Rate limiter replaced with no-op** in `ConfigureTestServices`. Cleaner than trying to remove `UseRateLimiter()` from the pipeline.
7. **Seeder left enabled.** It creates the admin user on first host start (when DB is empty after Respawn reset, the seeder won't re-run because the host only starts once per test run; Respawn wipes data but the host is already running). **Important nuance:** the seeder runs at host startup, before any test. Respawn then wipes the seeded admin before the first test. So the first test would have no admin to log in with. **Resolution:** seed the admin explicitly in `SplitDuoApiFactory.InitializeAsync` (after migrations, before tests start) via a direct `AppDbContext` insert, OR disable the hosted seeder and seed in the factory. See Implementation §3 for the chosen approach.

## Detailed Implementation Plan

### Step 1 — Update `SplitDuo.Tests.Integration.csproj`

Add the three missing packages. Keep existing references.

```xml
<ItemGroup>
  <PackageReference Include="Testcontainers.PostgreSql" Version="4.13.0" />
  <PackageReference Include="Respawn" Version="7.0.0" />
  <PackageReference Include="Npgsql" Version="10.0.0" />
</ItemGroup>
```

Note: `Testcontainers.PostgreSql` 4.13.0 requires the image passed to the constructor: `new PostgreSqlBuilder("postgres:17-alpine")` (parameterless constructor deprecated in 4.11.0).

### Step 2 — Podman environment setup

Create `sd-backend/run-integration-tests.sh` (executable):

```bash
#!/usr/bin/env bash
set -euo pipefail

# --- Podman socket (rootless) ---
# Auto-detect the per-user socket path
SOCKET_PATH="${DOCKER_HOST:-unix:///run/user/$(id -u)/podman/podman.sock}"
export DOCKER_HOST="$SOCKET_PATH"

# Ryuk (Testcontainers resource reaper) needs privileged mode under rootless podman
export TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED=true

# In-container socket path override (must match DOCKER_HOST for Ryuk to mount it)
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE="${SOCKET_PATH#unix://}"

# --- SplitDuo test config ---
export ASPNETCORE_ENVIRONMENT=Development
export SD_JWT_SECRET_KEY="test-integration-secret-key-32-chars-min!!"
export SD_JWT_ISSUER="test"
export SD_JWT_AUDIENCE="test"
export SD_SEED_DEMO_DATA=false

echo "DOCKER_HOST=$DOCKER_HOST"
echo "Running integration tests..."
exec dotnet test SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj \
  --verbosity normal \
  "$@"
```

**Verification before first run:** `podman info --format '{{.Host.RemoteSocket.Path}}'` to confirm the socket path. If rootless podman isn't running, start it: `systemctl --user start podman.socket` (or `podman system service --time=0`).

**Fallback if Ryuk fails under podman:** add `export TESTCONTAINERS_RYUK_DISABLED=true` (disables the resource reaper; containers are still cleaned up on dispose, just not orphaned-container reaping).

### Step 3 — `SplitDuoApiFactory.cs` (the test host)

**File:** `sd-backend/SplitDuo.Tests.Integration/SplitDuoApiFactory.cs` (new)

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using SplitDuo.Core.Domain.Entities;
using SplitDuo.Core.Extensions;            // AddInfrastructure lives here
using SplitDuo.Core.Options;
using SplitDuo.Core.Persistence;
using System.Security.Cryptography;
using System.Text;
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

            // --- C1: Replace rate limiter with a no-op ---
            // Remove the IConfigureOptions<RateLimiterOptions> registered by AddRateLimiter,
            // then re-add a permissive limiter.
            var rlConfigDescriptors = services
                .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var d in rlConfigDescriptors) services.Remove(d);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.CreateNoLimiter("test-bypass"));
            });

            // --- C2 (optional): Remove Quartz hosted service to avoid background job side effects ---
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

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

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
            TablesToIgnore = new[] { "__EFMigrationsHistory" },
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
        var user = new User
        {
            Guid = Guid.CreateVersion7(),
            Email = appOptions.InitialUserEmail,
            FirstName = appOptions.InitialUserFirstName,
            LastName = appOptions.InitialUserLastName,
            PasswordHash = HashPassword(appOptions.InitialUserPassword),
            GlobalRoleId = (int)GlobalRole.SystemAdmin,
            SecurityStamp = Guid.CreateVersion7().ToString(),
            Settings = new UserSettings(), // defaults: theme=auto, uiLanguage=en
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        // Match the app's password hashing (BCrypt via BCrypt.Net — verify the exact lib in PasswordHasher)
        // If the app uses a different hasher, mirror it here. See Open Question Q1.
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        // Respawn wipes all rows including the admin — re-seed for the next test
        await SeedAdminUserAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }
}
```

**Notes on the factory:**
- `Program.cs`'s `MigrateAsync().Wait()` runs during host construction (before `InitializeAsync`). It targets the overridden `AppDbContext` (Testcontainers DB) — so migrations apply to the container. The explicit `MigrateAsync` in `InitializeAsync` is idempotent (no-op if already applied).
- The seeder hosted service is removed because it races with `WebApplicationFactory` startup timing and would run before tests can control state. We seed explicitly in `InitializeAsync` and re-seed after each Respawn reset.
- `HashPassword` must match the app's `PasswordHasher` — see Open Question Q1.
- `GlobalRole` enum is in `SplitDuo.Core.Domain.Enums` (or similar) — confirm namespace during implementation.

### Step 4 — `IntegrationTest.cs` (base class)

**File:** `sd-backend/SplitDuo.Tests.Integration/IntegrationTest.cs` (new)

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Authentication.Dto;
using SplitDuo.Api.Features.Common.Dto;
using Xunit;

namespace SplitDuo.Tests.Integration;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<SplitDuoApiFactory> { }

[Collection("Integration")]
public abstract class IntegrationTest : IAsyncLifetime
{
    protected readonly SplitDuoApiFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTest(SplitDuoApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost"),
        });
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    /// <summary>
    /// Logs in via the real /auth/login endpoint and returns the JWT.
    /// Rate limiter is disabled in the test host, so repeated logins are safe.
    /// </summary>
    protected async Task<string> GetAuthTokenAsync(
        string email = "admin@localhost",
        string password = "changeme")
    {
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<AuthResponseDto>>();
        return body!.Data!.Token;
    }

    /// <summary>
    /// Returns an HttpClient with a valid Bearer token set.
    /// </summary>
    protected async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "admin@localhost",
        string password = "changeme")
    {
        var token = await GetAuthTokenAsync(email, password);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
```

**Notes:**
- `ApiResponseDto<T>` and `AuthResponseDto` are in `SplitDuo.Api.Features.Common.Dto` and `SplitDuo.Api.Features.Authentication.Dto` respectively — confirm exact namespaces during implementation.
- The login request body shape must match `LoginRequestDto` — if it uses `Email`/`Password` (PascalCase) the JSON serializer config matters. The app uses `JsonSerializerDefaults.Web` (camelCase) for API responses, but request deserialization is case-insensitive by default. Sending `{ email, password }` (camelCase) is safe.
- `AllowAutoRedirect = false` ensures we see 307s if HTTPS redirect somehow fires (it shouldn't in Development env, but defensive).

### Step 5 — `UserSettingsTests.cs` (the actual tests)

**File:** `sd-backend/SplitDuo.Tests.Integration/UserSettingsTests.cs` (new)

```csharp
using System.Net;
using System.Net.Http.Json;
using SplitDuo.Api.Features.Common.Dto;
using SplitDuo.Api.Features.Users.Dto;
using Xunit;

namespace SplitDuo.Tests.Integration;

public class UserSettingsTests : IntegrationTest
{
    public UserSettingsTests(SplitDuoApiFactory factory) : base(factory) { }

    // --- GET /users/me ---

    [Fact]
    public async Task GetCurrentUser_ReturnsDefaultSettings_ForSeededAdmin()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/users/me");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>();
        Assert.NotNull(body!.Data!.Settings);
        Assert.Equal("auto", body.Data.Settings!.Theme);
        Assert.Equal("en", body.Data.Settings.UiLanguage);
    }

    // --- PUT /users/me/settings: valid updates ---

    [Fact]
    public async Task UpdateTheme_PersistsAndReturnsUpdated()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>();
        Assert.Equal("dark", body!.Data!.Theme);
        Assert.Equal("en", body.Data.UiLanguage); // unchanged
    }

    [Fact]
    public async Task UpdateTheme_AppliesOnSubsequentGet()
    {
        var client = await CreateAuthenticatedClientAsync();

        await client.PutAsJsonAsync("/api/v1/users/me/settings", new { theme = "light" });

        var getResponse = await client.GetAsync("/api/v1/users/me");
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>();
        Assert.Equal("light", body!.Data!.Settings!.Theme);
    }

    [Fact]
    public async Task UpdateLanguage_Persists()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "en",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>();
        Assert.Equal("en", body!.Data!.UiLanguage);
    }

    [Fact]
    public async Task UpdateBoth_PersistsBoth()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
            uiLanguage = "en",
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>();
        Assert.Equal("dark", body!.Data!.Theme);
        Assert.Equal("en", body.Data.UiLanguage);
    }

    // --- PUT /users/me/settings: validation ---

    [Fact]
    public async Task UpdateTheme_InvalidValue_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "neon",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLanguage_InvalidValue_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            uiLanguage = "fr", // only "en" accepted in v1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- PUT /users/me/settings: auth ---

    [Fact]
    public async Task UpdateSettings_Unauthenticated_Returns401()
    {
        // Client has no Authorization header
        var response = await Client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_Unauthenticated_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PUT /users/me/settings: null = leave unchanged ---

    [Fact]
    public async Task UpdateSettings_EmptyBody_LeavesSettingsUnchanged()
    {
        var client = await CreateAuthenticatedClientAsync();

        // First set a known state
        await client.PutAsJsonAsync("/api/v1/users/me/settings", new { theme = "dark" });

        // Then send an empty body (both fields null)
        var response = await client.PutAsJsonAsync("/api/v1/users/me/settings", new { });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<UserSettingsDto>>();
        Assert.Equal("dark", body!.Data!.Theme); // unchanged from previous PUT
    }

    // --- jsonb round-trip (the core feature assertion) ---

    [Fact]
    public async Task Settings_RoundTripThroughJsonb_PreservesValues()
    {
        var client = await CreateAuthenticatedClientAsync();

        // Write
        await client.PutAsJsonAsync("/api/v1/users/me/settings", new
        {
            theme = "dark",
            uiLanguage = "en",
        });

        // Read back via a fresh request (forces DB round-trip, not in-memory cache)
        var getResponse = await client.GetAsync("/api/v1/users/me");
        getResponse.EnsureSuccessStatusCode();
        var body = await getResponse.Content.ReadFromJsonAsync<ApiResponseDto<UserDto>>();

        Assert.Equal("dark", body!.Data!.Settings!.Theme);
        Assert.Equal("en", body.Data.Settings.UiLanguage);
    }
}
```

### Step 6 — `Usings.cs` (global usings)

**File:** `sd-backend/SplitDuo.Tests.Integration/Usings.cs` (new)

```csharp
global using Microsoft.AspNetCore.Mvc.Testing;
global using Xunit;
```

### Step 7 — `.gitignore` (if needed)

Check whether `sd-backend/.gitignore` already excludes bin/obj. If not, the test project's build artifacts should be ignored (likely already covered by the repo root `.gitignore`).

## Test Cases Summary

| # | Test | Method | Endpoint | Asserts |
|---|---|---|---|---|
| 1 | `GetCurrentUser_ReturnsDefaultSettings_ForSeededAdmin` | GET | `/users/me` | 200, `settings.theme == "auto"`, `settings.uiLanguage == "en"` |
| 2 | `UpdateTheme_PersistsAndReturnsUpdated` | PUT | `/users/me/settings` | 200, `theme == "dark"`, `uiLanguage == "en"` (unchanged) |
| 3 | `UpdateTheme_AppliesOnSubsequentGet` | PUT→GET | `/users/me/settings`→`/users/me` | GET returns the just-PUT theme |
| 4 | `UpdateLanguage_Persists` | PUT | `/users/me/settings` | 200, `uiLanguage == "en"` |
| 5 | `UpdateBoth_PersistsBoth` | PUT | `/users/me/settings` | 200, both fields updated |
| 6 | `UpdateTheme_InvalidValue_Returns400` | PUT | `/users/me/settings` | 400 (theme="neon") |
| 7 | `UpdateLanguage_InvalidValue_Returns400` | PUT | `/users/me/settings` | 400 (uiLanguage="fr") |
| 8 | `UpdateSettings_Unauthenticated_Returns401` | PUT | `/users/me/settings` | 401 (no auth header) |
| 9 | `GetCurrentUser_Unauthenticated_Returns401` | GET | `/users/me` | 401 (no auth header) |
| 10 | `UpdateSettings_EmptyBody_LeavesSettingsUnchanged` | PUT | `/users/me/settings` | 200, settings unchanged from prior PUT |
| 11 | `Settings_RoundTripThroughJsonb_PreservesValues` | PUT→GET | `/users/me/settings`→`/users/me` | jsonb round-trip preserves both fields |

## Verification Plan

### Build verification
```bash
cd sd-backend && dotnet build SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj
```
Expected: 0 warnings, 0 errors.

### Podman socket verification (before first test run)
```bash
podman info --format '{{.Host.RemoteSocket.Path}}'
# Should output something like: unix:///run/user/1000/podman/podman.sock
# If empty, start the socket: systemctl --user start podman.socket
```

### First test run
```bash
cd sd-backend && ./run-integration-tests.sh
```
Expected: container starts (~3s), migrations apply, 11 tests pass (<5s total).

### What "done" looks like
- `dotnet build` clean for the integration test project.
- `./run-integration-tests.sh` runs all 11 tests green against a podman-spawned PostgreSQL 17 container.
- The harness (`SplitDuoApiFactory` + `IntegrationTest`) is reusable for future endpoint tests — adding a new test class only requires extending `IntegrationTest` and writing `[Fact]` methods.

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Podman rootless socket not running | Medium | Blocks all tests | Script checks `podman info`; document `systemctl --user start podman.socket` in script header |
| Ryuk container fails under rootless podman | Medium | Tests fail to start | Fallback: `TESTCONTAINERS_RYUK_DISABLED=true` (containers still cleaned on dispose) |
| `MigrateAsync().Wait()` in `Program.cs` runs before container is ready | Low | Host construction fails | `WebApplicationFactory` constructs the host lazily; `ConfigureTestServices` swaps `AppDbContext` before `Program.cs`'s `MigrateAsync` runs — but the container must be started first. **Order:** `SplitDuoApiFactory` constructor creates the container object (doesn't start it); `WebApplicationFactory` calls `ConfigureWebHost` during construction; `Program.cs` runs `MigrateAsync` during `app.Build()`/`app.Run()`. **Critical timing issue:** the container isn't started until `InitializeAsync`, but `Program.cs` runs during host construction (before `InitializeAsync`). **Fix:** start the container in `ConfigureWebHost` (synchronously block) OR override the migration call. See Open Question Q2 — this is the highest-risk implementation detail. |
| Password hashing mismatch (test seed vs app hasher) | Medium | Login fails (401) | Mirror the app's `PasswordHasher` exactly in `SeedAdminUserAsync`. Confirm the lib (BCrypt.Net? Argon2? PBKDF2?) during implementation. |
| `Program` class not accessible from test project | Low | `WebApplicationFactory<Program>` won't compile | Top-level statements generate an internal `Program` class; the `ProjectReference` may need `<InternalsVisibleTo>` in `SplitDuo.Api.csproj`. Add if compile fails. |
| Quartz jobs firing during tests | Low | Side effects (email send attempts) | Removed in `ConfigureTestServices` (Step 3). If removal is fragile (descriptor matching), set `SmtpOptions.SmtpServer` to empty so sends fail fast. |
| Respawn doesn't reset the `settings` jsonb column | Very Low | Test isolation breaks | Respawn treats jsonb as a regular column — truncates the row, not the column value. Confirmed supported in Respawn 7.0.0. |
| Test host environment not "Development" | Low | HTTPS redirect fires, Serilog PG sink activates | Explicit `builder.UseEnvironment("Development")` in `ConfigureWebHost`. |

## Open Questions (resolve during implementation)

1. **Password hashing library.** The explorer didn't confirm which library `DataSeederService` / `PasswordHasher` uses (BCrypt.Net, Argon2, PBKDF2?). `SeedAdminUserAsync` in the factory must produce a hash that the login endpoint accepts. **Action:** read `SplitDuo.Core/Services/PasswordHasher.cs` (or wherever password hashing lives) and mirror it exactly. If it's BCrypt, the snippet above works; if Argon2, swap the call.
2. **Container startup timing vs `Program.cs` `MigrateAsync`.** This is the highest-risk implementation detail. `WebApplicationFactory` constructs the host (running `Program.cs`, including `MigrateAsync().Wait()`) **before** `IAsyncLifetime.InitializeAsync` runs. If the container isn't started yet, `MigrateAsync` will fail to connect. **Two options:**
   - **(a)** Start the container synchronously in `ConfigureWebHost` (before `ConfigureTestServices`): `((IAsyncLifetime)this).InitializeAsync()` won't work there. Instead, use a `PostgreSqlContainer` that's started in a static/field initializer or a lazy pattern. Cleanest: override `CreateHost` or use `IHostBuilder`'s `ConfigureServices` with a container that's started in the factory constructor via `.GetAwaiter().GetResult()` (blocking, but only once).
   - **(b)** Remove the `MigrateAsync().Wait()` from `Program.cs` by making it conditional on a config flag (e.g., `SD_AUTO_MIGRATE=false` skips it). This is a small production-code change but makes the app more test-friendly. **Preferred if the team accepts it.**
   - **Recommendation:** try (a) first (no production code change); fall back to (b) if timing proves fragile.
3. **`Program` class visibility.** If `WebApplicationFactory<Program>` fails to compile (`Program` is internal), add `<InternalsVisibleTo Include="SplitDuo.Tests.Integration" />` to `SplitDuo.Api.csproj`. Likely needed — verify on first build.
4. **`GlobalRole` enum namespace.** The factory references `GlobalRole.SystemAdmin` — confirm the namespace (`SplitDuo.Core.Domain.Enums`?) during implementation.
5. **`ApiResponseDto` / `AuthResponseDto` / `UserDto` namespaces.** The test base class and tests reference these DTOs — confirm exact namespaces (`SplitDuo.Api.Features.Common.Dto`, `SplitDuo.Api.Features.Authentication.Dto`, `SplitDuo.Api.Features.Users.Dto`).

## CLAUDE.md Sync (per repo rule #6)

After implementation, add to `sd-backend/CLAUDE.md` under "Rules When Modifying This Code":

> - **Integration tests** — live in `SplitDuo.Tests.Integration`, use `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (`postgres:17-alpine`) via podman. Run with `./run-integration-tests.sh` (sets `DOCKER_HOST` + Ryuk env vars). The `SplitDuoApiFactory` (`ICollectionFixture`) overrides `AppDbContext` to the container, replaces the rate limiter with a no-op, removes Quartz + seeder hosted services, and seeds the admin user explicitly. `IntegrationTest` base class provides `CreateAuthenticatedClientAsync` (real login). Respawn resets the DB between tests.

## File Manifest (to create/modify)

| File | Action | Purpose |
|---|---|---|
| `sd-backend/SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj` | Modify | Add Testcontainers.PostgreSql, Respawn, Npgsql |
| `sd-backend/SplitDuo.Tests.Integration/SplitDuoApiFactory.cs` | New | Test host: container, DB override, rate limiter no-op, seeder, Respawner |
| `sd-backend/SplitDuo.Tests.Integration/IntegrationTest.cs` | New | Base class: HttpClient, auth helper, Respawn reset on dispose |
| `sd-backend/SplitDuo.Tests.Integration/UserSettingsTests.cs` | New | 11 tests for the settings feature |
| `sd-backend/SplitDuo.Tests.Integration/Usings.cs` | New | Global usings (WebApplicationFactory, Xunit) |
| `sd-backend/run-integration-tests.sh` | New | Podman env vars + `dotnet test` launcher |
| `sd-backend/CLAUDE.md` | Modify | Add integration test rule |

## Out of Scope / Future Work

- **Unit tests** in `SplitDuo.Tests.Unit` — separate effort; the harness doesn't apply.
- **More integration tests** for other endpoints (auth, groups, expenses, imports) — the harness is reusable; future tests extend `IntegrationTest`.
- **CI pipeline integration** — the `run-integration-tests.sh` script works locally; CI (GitLab CI) would need podman in the runner image and the same env vars.
- **Testcontainers reuse** (`WithReuse()`) to keep the container alive across runs — disabled when Ryuk is disabled; not worth the complexity for now.
- **Parallel test classes** — xUnit v3 runs classes in parallel by default; the `ICollectionFixture` serializes tests in the collection. If parallelism is needed later, use multiple collections with separate containers.
- **Frontend E2E tests** (Playwright/Cypress) — entirely separate effort.