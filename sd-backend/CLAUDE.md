# CLAUDE.md — sd-backend

## Overview

.NET 10 REST API with vertical slice architecture. Two-project solution: `SplitDuo.Api` (controllers, DTOs, API services) and `SplitDuo.Core` (entities, data access, infrastructure). PostgreSQL via EF Core, JWT auth with refresh token rotation, background jobs via Quartz.NET.

## Commands

```bash
dotnet restore                                                  # Restore packages
dotnet run --project SplitDuo.Api                               # Dev server on http://localhost:5000
dotnet ef migrations add <Name> --project SplitDuo.Api          # Create migration
dotnet ef database update --project SplitDuo.Api                # Apply migrations
```

Migrations auto-apply on startup (`Database.MigrateAsync()` in Program.cs).

## Project Structure

```
sd-backend/
├── SplitDuo.Api/
│   ├── Program.cs                          # Entry point (AddServices → ConfigureServices → auto-migrate)
│   ├── Extensions/
│   │   └── ApiProgramExtensions.cs         # DI registration + middleware pipeline
│   └── Features/                           # Vertical slices
│       ├── Authentication/
│       │   ├── Controllers/                # AuthController, TwoFactorController
│       │   ├── Dto/                        # Login/Register/2FA DTOs
│       │   └── Services/                   # AuthenticationService, TwoFactorService
│       ├── Categories/
│       │   └── Controllers/                # Enum-based, no DB table
│       ├── Common/
│       │   ├── Controllers/BaseApiController.cs  # Base class for all controllers
│       │   └── Dto/                        # ApiResponseDto, PaginationDto, UserBasicInfoDto
│       ├── Expenses/
│       │   ├── Controllers/
│       │   ├── Dto/
│       │   └── Services/
│       ├── Exports/
│       │   ├── Controllers/
│       │   └── Services/
│       ├── Groups/
│       │   ├── Controllers/                # GroupsController, GroupMembersController
│       │   ├── Dto/
│       │   └── Services/
│       ├── Imports/
│       │   ├── Controllers/
│       │   └── Dto/
│       ├── PaymentModes/
│       │   └── Controllers/                # Enum-based, no DB table
│       ├── Settlements/
│       │   ├── Controllers/
│       │   ├── Dto/
│       │   └── Services/
│       └── Users/
│           ├── Controllers/
│           ├── Dto/
│           └── Services/
│
└── SplitDuo.Core/
    ├── Common/
    │   ├── Result.cs                       # Result<T> pattern for service returns
    │   ├── HashUtils.cs                    # SHA256 hashing
    │   └── FileUtils.cs
    ├── Domain/
    │   ├── Base/                           # AuditableEntity, AuditableAndSoftDeletableEntity
    │   ├── Entities/                       # User, Group, GroupMember, Expense, ExpenseSplit,
    │   │                                   # Settlement, RefreshToken, TwoFactorToken,
    │   │                                   # Notification, Import
    │   ├── Enums/                          # GlobalRole, GroupRole, ExpenseCategory,
    │   │                                   # PaymentMode, ImportStatus, ImportType
    │   └── Interfaces/                     # Service interfaces
    ├── Dto/                                # Shared DTOs (import analysis, mappings)
    ├── Exceptions/
    │   └── GlobalExceptionHandler.cs       # IExceptionHandler → ProblemDetails
    ├── Factories/
    │   └── ImportServiceFactory.cs         # Resolves import service by ImportType
    ├── Migrations/                         # EF Core migrations
    ├── Options/
    │   ├── AppOptions.cs                   # App config (base URL, initial user)
    │   ├── DatabaseOptions.cs              # DB connection (builds connection string)
    │   ├── JwtOptions.cs                   # JWT secret, expiration
    │   ├── SmtpOptions.cs                  # Email config
    │   └── Setup/                          # IConfigureOptions<T> implementations
    ├── Persistence/
    │   ├── AppContext.cs                    # DbContext with all DbSets
    │   ├── UnitOfWork.cs                   # IUnitOfWork wrapping AppContext
    │   └── Interceptors/
    │       ├── AuditSaveChangesInterceptor.cs      # Auto-set CreatedAt/UpdatedAt
    │       └── SoftDeleteSaveChangesInterceptor.cs  # Convert Delete → set DeletedAt
    └── Services/
        ├── BackgroundJobs/                 # Quartz jobs (email, cleanup, import)
        ├── Exports/                        # CSV/Cospend export
        ├── Imports/                        # Abstract base + Cospend/SplitDuo importers
        ├── DataSeederService.cs            # Seeds initial admin user
        ├── EmailNotificationService.cs     # Email queue (enqueue → background send)
        └── SmtpService.cs                  # MailKit SMTP client
```

## Key Patterns

### Vertical Slices

Each feature in `Features/` has `Controllers/`, `Dto/`, and optionally `Services/`. A feature owns its full stack from HTTP endpoint to business logic. Cross-cutting concerns live in `SplitDuo.Core`.

### Result Pattern

Services return `Result<T>` — never throw for business errors:

```csharp
public static Result<T> Success(T value, HttpStatusCode statusCode = HttpStatusCode.OK);
public static Result<T> BadRequest(string error);
public static Result<T> NotFound(string error);
public static Result<T> Unauthorized(string error);
public static Result<T> Forbidden(string error);
```

Controllers call `HandleResult(result, "message")` which maps `Result` status codes to HTTP responses wrapped in `ApiResponseDto<T>`.

### Controller → Service → UnitOfWork Flow

```csharp
[HttpPost]
public async Task<ActionResult<ApiResponseDto<GroupDto>>> Create([FromBody] CreateGroupDto dto)
{
    var result = await groupsService.CreateAsync(dto);
    if (result.IsSuccess) await unitOfWork.SaveChangesAsync();  // Controller saves
    return HandleResult(result, "Group created");
}
```

Services **do not** call `SaveChangesAsync()` — the controller does, after checking `result.IsSuccess`. This keeps transaction control at the controller level.

### API Response Shape

```json
// Success
{ "success": true, "data": { ... }, "message": "...", "error": null }

// Error
{ "success": false, "data": null, "error": { "code": "NOT_FOUND", "message": "...", "details": [] } }

// Paginated
{ "success": true, "data": [...], "pagination": { "page": 1, "limit": 20, "total": 150, "totalPages": 8, "hasNext": true, "hasPrev": false } }
```

### BaseApiController

All controllers inherit from `BaseApiController` which provides:
- `GetCurrentUserId()`, `GetCurrentUserAsync()`, `IsCurrentUserSystemAdmin()` — via `IUserContextService`
- `HandleResult<T>(result, message)` — maps `Result<T>` to `ActionResult<ApiResponseDto<T>>`
- `HandlePaginatedResult<T>(result, pagination)` — same for paginated responses

### DTO Mapping

Manual mapping — no AutoMapper. DTOs have constructors that accept entities:

```csharp
public class ExpenseDto
{
    public ExpenseDto(Expense expense, List<ExpenseSplit>? splits = null)
    {
        Id = expense.Guid.ToString();  // Guid exposed, int stays internal
        // ...
    }
}
```

**ID convention**: Entities have `int Id` (DB primary key) + `Guid Guid` (API-facing). DTOs expose `Guid` as `string`.

## Entity Model

### Base Classes

- **`AuditableEntity`** — `int Id`, `Guid Guid`, `long CreatedAt`, `long UpdatedAt` (Unix timestamps)
- **`AuditableAndSoftDeletableEntity`** — adds `long? DeletedAt`

### Entities

| Entity | Key Fields | Notes |
|---|---|---|
| `User` | Email (unique), PasswordHash, GlobalRole, 2FA fields | Soft-deletable |
| `Group` | Name, Description, CreatedByUserId | Soft-deletable |
| `GroupMember` | GroupId, UserId, GroupRole | |
| `Expense` | Title, Amount, ExpenseDate (DateOnly), CategoryId, PaymentModeId, PaidByUserId | Soft-deletable |
| `ExpenseSplit` | ExpenseId, UserId, SplitAmount | |
| `Settlement` | GroupId, FromUserId, ToUserId, Amount, Date | Soft-deletable |
| `RefreshToken` | TokenHash (SHA256), JwtId, ExpiresAt, RevokedAt | Token rotation chain |
| `TwoFactorToken` | TokenHash, TokenType, Purpose, MaxAttempts, Attempts | Rate-limited |
| `Notification` | To, Subject, Body (HTML), SentAt, RetryCount | Email queue |
| `Import` | ImportType, Status, TempFile (byte[]), RecordsCount | File cleared post-processing |

### Enums

- `GlobalRole` — BaseUser (1), SystemAdmin (2)
- `GroupRole` — Member, Admin
- `ExpenseCategory` — Other, Groceries, Transportation, Utilities, Entertainment, Health, Education, Travel, Shopping, Housing, Dining
- `PaymentMode` — Other, Card, Cash, Transfer, OnlineService
- `ImportStatus` — Pending, Processing, Completed, Failed
- `ImportType` — Cospend, SplitDuo

### EF Interceptors

- **AuditSaveChangesInterceptor** — auto-sets `CreatedAt` on insert, `UpdatedAt` on insert/update (Unix timestamps)
- **SoftDeleteSaveChangesInterceptor** — intercepts `EntityState.Deleted`, converts to `Modified` + sets `DeletedAt`

## Authentication & Security

### JWT

- 15-minute access tokens, 7-day refresh tokens
- Claims: `sub` (user GUID), `email`, `firstName`, `lastName`, `role` (GlobalRole int)
- Secret from `SD_JWT_SECRET_KEY` env var
- Issuer/audience validation disabled

### Refresh Token Rotation

- On refresh: old token revoked, new token issued, `ReplacedByToken` tracks chain
- **Reuse detection**: if a revoked token is reused → all user tokens revoked (compromised session)
- New login revokes all existing refresh tokens

### 2FA

Three methods: TOTP (authenticator apps), email codes (6-digit, 10-min expiry), backup codes (10 one-time, hashed).

Login with 2FA: password check → `RequiresTwoFactor: true` (no tokens) → user submits code → tokens issued.

### Authorization

Single policy: `"SystemAdmin"` — checks `ClaimTypes.Role == GlobalRole.SystemAdmin`. Applied with `[Authorize(Policy = "SystemAdmin")]`.

Group membership checks are done in services (not policies).

### Password Reset

Token stored as SHA256 hash in `TwoFactorToken` (purpose: "password_reset", 1-hour expiry, max 3 attempts). Always returns success to prevent email enumeration.

## Configuration (Options Pattern)

All config uses `IConfigureOptions<T>` — env vars override appsettings.json:

| Options Class | Key Env Vars |
|---|---|
| `AppOptions` | `SD_BASE_URL`, `SD_INITIAL_USER_EMAIL`, `SD_INITIAL_USER_PASSWORD` |
| `DatabaseOptions` | `SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME`, `SD_DB_USERNAME`, `SD_DB_PASSWORD` |
| `JwtOptions` | `SD_JWT_SECRET_KEY`, `SD_JWT_EXPIRES` |
| `SmtpOptions` | `SD_SMTP_SERVER`, `SD_SMTP_PORT`, `SD_SMTP_USERNAME`, `SD_SMTP_PASSWORD` |

## Dependency Injection

Services registered in two extension classes:

- **`SplitDuo.Core/Extensions/ApiProgramExtensions.cs`** — DbContext, UnitOfWork, interceptors, options, Quartz, auth, core services
- **`SplitDuo.Api/Extensions/ApiProgramExtensions.cs`** — Controllers, OpenAPI, feature services (scoped), keyed import services

All feature services are `Scoped`. Import services use **keyed DI**:
```csharp
builder.Services.AddKeyedScoped<IImportsService, CospendImportsService>(ImportType.Cospend);
builder.Services.AddKeyedScoped<IImportsService, SplitDuoImportsService>(ImportType.SplitDuo);
```

## Background Jobs (Quartz.NET)

| Job | Schedule | Purpose |
|---|---|---|
| `EmailNotificationProcessingJob` | Every 2 minutes | Send queued emails (max 3 retries) |
| `EmailNotificationPruneJob` | Daily 01:00 | Delete sent emails > 30 days |
| `LogCleanupJob` | Daily 02:00 | Delete old Serilog DB entries |
| `TempFileCleanupJob` | Daily 04:00 | Delete orphaned temp files |
| `ImportProcessingJob` | On-demand | Process CSV import (triggered by user action) |

In-memory job store, max 5 concurrent threads.

## Import/Export

### Import (Two-Phase)

1. **Analyze** — Parse CSV, validate structure, compute hash, return unmatched users/categories
2. **Map** — User provides mappings for unmatched entities
3. **Process** — Quartz job creates expenses in background. File stored as `byte[]` in `Import.TempFile`, cleared after processing.

Factory pattern resolves importer by `ImportType`. Abstract base class (`AbstractImportService`) handles common flow, concrete classes (`CospendImportsService`, `SplitDuoImportsService`) implement `ProcessImportAsync()`.

### Export

CSV and Cospend JSON formats. Uses CsvHelper. Owers encoded as `email:amount|email:amount`.

## Email

MailKit for SMTP. Async queue pattern: `EnqueueAsync()` adds to `Notifications` table → Quartz job sends every 2 minutes with retry (max 3). Used for 2FA codes, password resets, and confirmations.

## Middleware Pipeline

```
ExceptionHandler → OpenAPI/Scalar (dev) → CORS (dev) → HTTPS Redirect
→ Serilog Request Logging → Static Files (wwwroot) → Authentication
→ Authorization → Controllers → Fallback to index.html (SPA)
```

No custom middleware — all built-in ASP.NET Core.

## Logging (Serilog)

Console always. PostgreSQL sink in production (batched every 30s, table: `logging.logs`, auto-created). Request logging via `UseSerilogRequestLogging()`.

## Database Seeding

`DataSeederService` (IHostedService) runs on startup. If no users exist, creates initial SystemAdmin user from `AppOptions` (email, password from env vars). Default: `admin@localhost` / `changeme`.

## Rules When Modifying This Code

- **Follow vertical slice pattern** — new features get their own folder under `Features/` with `Controllers/`, `Dto/`, `Services/`
- **Return `Result<T>` from services** — never throw for business errors, never return HTTP types
- **Controller saves** — call `unitOfWork.SaveChangesAsync()` in the controller after `result.IsSuccess`, not in the service
- **Manual DTO mapping** — no AutoMapper, use constructors that accept entities
- **Expose Guid, not int** — DTOs use `Guid.ToString()` for IDs
- **Use `IUnitOfWork`** for data access — don't inject `AppContext` directly in services
- **Register services in the correct extension class** — Core infra in `Core/Extensions`, feature services in `Api/Extensions`
- **Use Options pattern** for configuration — never read `IConfiguration` directly in services
- **Use `INotificationService.EnqueueAsync()`** for emails — never call `SmtpService` directly
- **Timestamps are Unix `long`** — use `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`
