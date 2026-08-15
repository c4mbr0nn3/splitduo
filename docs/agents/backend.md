# Backend Guide — sd-backend

> Read this before modifying `sd-backend/`. Canonical source for backend conventions, patterns, and rules.

## Overview

.NET 10 REST API, vertical slice architecture. `SplitDuo.Api` (controllers, DTOs) + `SplitDuo.Core` (entities, data, infrastructure). PostgreSQL via EF Core, JWT auth with refresh rotation, Quartz.NET background jobs.

## Commands

- `dotnet run --project SplitDuo.Api` — dev on http://localhost:8080
- `dotnet ef migrations add <Name> --project SplitDuo.Core --startup-project SplitDuo.Api`
- Migrations auto-apply on startup (`Database.MigrateAsync()` in Program.cs)

## Structure

- `SplitDuo.Api/Features/` — vertical slices: `Aliases`, `Authentication`, `Categories`, `Common`, `Expenses`, `Exports`, `Groups`, `Imports`, `Invitations`, `PaymentModes`, `Receipts`, `Settlements`, `Users`, `Ai`
- Each slice: `Controllers/`, `Dto/`, optionally `Services/`
- `SplitDuo.Core/` — `Domain/` (entities, enums, interfaces), `Persistence/` (AppContext, UnitOfWork, interceptors), `Services/` (background jobs, imports, exports, email), `Options/` (IConfigureOptions pattern)
- `Common/Result.cs` — Result<T> pattern
- DI split: `Core/Extensions/ApiProgramExtensions.cs` (infra) + `Api/Extensions/ApiProgramExtensions.cs` (feature services)

## Key Patterns

### Vertical Slices
Each feature in `Features/` has `Controllers/`, `Dto/`, and optionally `Services/`. A feature owns its full stack from HTTP endpoint to business logic. Cross-cutting concerns live in `SplitDuo.Core`.

### Result Pattern
Services return `Result<T>` — never throw for business errors. Controllers call `HandleResult(result, "message")` which maps `Result` status codes to HTTP responses wrapped in `ApiResponseDto<T>`.

### Controller → Service → UnitOfWork Flow
Services **do not** call `SaveChangesAsync()` — the controller does, after checking `result.IsSuccess`. This keeps transaction control at the controller level.

### API Response Shape
`{ success, data, message?, error?, pagination? }` — see `ApiResponseDto<T>` for full shape.

### DTO Mapping
Manual mapping — no AutoMapper. DTOs have constructors that accept entities. **ID convention**: Entities have `int Id` (DB primary key) + `Guid Guid` (API-facing). DTOs expose `Guid` as `string`.

## Entity Model

Base classes: `AuditableEntity` (int Id, Guid Guid, long CreatedAt, long UpdatedAt — Unix seconds) → `AuditableAndSoftDeletableEntity` (+ long? DeletedAt).

Soft-deletable: User, Group, Expense, Settlement, Alias. Others: GroupMember, ExpenseSplit, ExpenseAliasSplit, RefreshToken, TwoFactorToken, Notification, InvitationToken, Import, AiCallLog.

Key conventions:
- Tokens (refresh, 2FA, invitation) stored as SHA256 hash, never plaintext
- `InvitationToken`: 48h expiry, `IsPending` computed, accept resolves ALL pending invitations for that email
- `Import.TempFile` (byte[]) cleared after processing
- `RefreshToken`: rotation chain via `ReplacedByToken`, reuse detection revokes all user tokens
- **Alias mode**: `Group.UseAliases` (immutable after creation) + `Group.AliasSetupFinalized` (admin-locked after setup). `Alias` (name, optional `IsSingleton`) groups `GroupMember`s; `Expense.PaidByAliasId` + `ExpenseAliasSplit` rows drive alias-level balances. Removing a member from an alias auto-creates a singleton alias. Expenses blocked until finalized.

Enums: `GlobalRole` (BaseUser=1, SystemAdmin=2), `GroupRole` (Member, Admin), `ExpenseCategory` (11 values), `PaymentMode` (5 values), `ImportStatus`, `ImportType` (Cospend, SplitDuo, Splitwise, SplitDuoAlias), `EmailTemplate` (11 values).

EF interceptors: `AuditSaveChangesInterceptor` (auto CreatedAt/UpdatedAt), `SoftDeleteSaveChangesInterceptor` (Delete → Modified + DeletedAt).

## Auth & Security

- JWT: 15-min access, 7-day refresh. Claims: sub (user GUID), email, firstName, lastName, role (GlobalRole int). Issuer/audience validation disabled.
- Refresh rotation: old token revoked on refresh, ReplacedByToken chain. Reuse of revoked token → all user tokens revoked. New login revokes all existing.
- 2FA: TOTP + email codes (6-digit, 10-min) + backup codes (10, hashed). Login: password → RequiresTwoFactor flag → code → tokens.
- Authorization: single `"SystemAdmin"` policy. Group membership checked in services, not policies.
- Password reset: SHA256 token in TwoFactorToken (1h expiry, max 3 attempts). Always returns success (prevent email enumeration).

## Invitations

- Existing user: admin types email → user found → added as GroupMember → notification sent
- New user: no user found → InvitationToken (SHA256, 48h) → email with registration link
- Accept: validates token → creates account → resolves ALL pending invitations for that email across all groups
- `AcceptInvitation` uses explicit transaction (intermediate save for User.Id before GroupMember insert)
- Resend: revokes old token, creates new one

## Configuration

Options pattern via `IConfigureOptions<T>` in `Core/Options/Setup/`. Env vars override appsettings. Classes: `AppOptions`, `DatabaseOptions`, `JwtOptions`, `SmtpOptions`, `AiOptions`. See `Setup/` folder for env var names.

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

Import (two-phase): Analyze (parse CSV, return unmatched) → Map (user provides mappings) → Process (Quartz job, background). Factory resolves importer by ImportType (keyed DI). `AbstractImportService` base; concrete: `CospendImportsService`, `SplitDuoImportsService`, `SplitwiseImportsService`, `SplitDuoAliasImportsService` (three-section CSV: aliases, members, expenses; `SplitDuoAliasCsvParser`).

Export: CSV only, CsvHelper. Normal mode: owers encoded as `email:amount|email:amount`. Alias mode (`group.UseAliases`): three-section CSV (aliases, members, expenses); alias splits encoded as `aliasName:amount|aliasName:amount`.

## Email

MailKit SMTP. Queue pattern: `INotificationService.EnqueueAsync()` → Notifications table → Quartz job sends every 2 min (max 3 retries). **Never call SmtpService directly** — use INotificationService.

### Email Template Rules (`EmailTemplateProvider.cs`)

| Concern | Rule |
|---|---|
| **Subject** | No app-name prefix; action-oriented verbs; include dynamic content (group name, actor, code) where meaningful |
| **Greeting** | `Hi {FirstName},` for named recipients; `Hi there,` for anonymous (GroupInvitation) |
| **Sign-off** | `<p>— The SplitDuo Team</p>` on every email |
| **Security footer** | `<p>Didn't do this? Contact support immediately — your account may be at risk.</p>` on all password and 2FA change notifications |
| **CTAs** | First-person link text: "Reset my password", "Accept invitation", "View group" |
| **Expense line** | `{Title} &mdash; {amount:F2} &middot; {date:MMMM d, yyyy}` |
| **Body copy** | One purpose per paragraph; no verbose preambles ("This is a security notification to inform you that…") |

## Seeding

`DataSeederService` (IHostedService): if no users exist, creates SystemAdmin from AppOptions. Defaults (no env): `admin@splitduo.local` / `changeme123` (firstname "Super", lastname "Admin").

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
- **Soft-delete, don't hard-delete** — entities inheriting AuditableAndSoftDeletableEntity are soft-deleted by the interceptor; never manually set DeletedAt
- **Hash tokens before storage** — use HashUtils.Sha256() for refresh, 2FA, invitation tokens; never store raw tokens
- **AI features are optional** — guard with AiOptions.IsEnabled; receipt scanning silently unavailable if AI not configured
- **Quartz jobs are in-memory** — job state lost on restart; design jobs to be idempotent
- **JSON columns** — use `ComplexProperty().ToJson()` (in `AppContext.OnModelCreating`) for hot/evolving per-entity blobs like user settings; reserve the `[Column(TypeName = "jsonb")]` + manual `JsonSerializer` pattern (see `Import`) for cold, write-once metadata. Add new settings as properties with default initializers on the POCO — no migration needed for additive changes.
- **Integration tests** — live in `SplitDuo.Tests.Integration`, use `WebApplicationFactory<Program>` + Testcontainers PostgreSQL (`postgres:17-alpine`) via podman. Run with `./run-integration-tests.sh` (sets `DOCKER_HOST` + Ryuk env vars). The `SplitDuoApiFactory` (`ICollectionFixture`) overrides `AppDbContext` to the container, replaces the rate limiter with a no-op, removes Quartz + seeder hosted services, and seeds the admin user explicitly. `IntegrationTest` base class provides `CreateAuthenticatedClientAsync` (real login). Respawn resets the DB between tests.
- **Prefer TDD where practical** — write the test first (or alongside) for new features and bug fixes. Integration tests are the primary tool; the red-green loop is slower (container startup) so "test-first" rather than strict TDD is the bar. Skip TDD for things that are hard to test in isolation (migrations, EF config, DI wiring).
- **Add tests when modifying untested code** — when changing behavior in code that lacks test coverage, add integration tests covering the behavior you're modifying. Scope tests to the change, not the whole file.
- **Verify before commit** — run `dotnet build` before every commit (catches compile errors). Run `./run-integration-tests.sh` before push/merge or when touching code that has existing tests. Never commit if the build fails.
- **Tests must surface bugs, never hide them** — a test covering buggy behavior must assert the CORRECT behavior and be allowed to FAIL, not be tweaked to pass against the buggy output. A passing test that documents current buggy behavior is an anti-pattern: it hides the bug and silences the regression signal. Mark such tests with a `// BUG: ...` comment explaining the defect and that the test is EXPECTED TO FAIL until the bug is fixed. Never use `[Fact(Skip = ...)]` for this — skipped tests are invisible. The failing test is the tracker.
- **Validate enum values from JSON payloads** — `System.Text.Json` accepts out-of-range integers for enum fields by default (e.g., `{ "globalRole": 99 }` deserializes to `(GlobalRole)99` without error). Always add `Enum.IsDefined(value)` check in the service after `HasValue`/null checks, or use `[Range]` validation on the DTO. String-based enum fields parsed via `Enum.TryParse` are safe (reject invalid strings), but numeric enum fields are not.
- **Last-admin guard ordering** — when implementing role-change guards, the self-demotion guard (`currentUserId == targetUserId`) makes the last-admin guard (`adminCount <= 1`) unreachable via the API: if the caller is a different admin, `adminCount >= 2`. Keep the last-admin guard as defense-in-depth, but don't write tests that expect to trigger it through the normal API path — test the "demote one of two admins succeeds" scenario instead.
- **i18n — all user-facing strings via `IStringLocalizer<T>`** — never hardcode English in `Result.BadRequest/Unauthorized/NotFound` calls or validation attributes. Inject `IStringLocalizer<ServiceName>` and use `_loc["Key"]`. `.resx` files live in `Resources/{namespace-path}/{TypeName}.{culture}.resx` (en base + .it). Core services need `[assembly: ResourceLocation("Resources")]` + `[assembly: RootNamespace("SplitDuo.Core")]` in `Properties/AssemblyInfo.cs`. Keep en/it key sets identical.
- **i18n — JWT `lang` claim** — issued at login from `UserSettings.UiLanguage`, re-issued by the settings endpoint when `uiLanguage` changes. Culture resolution: custom `RequestCultureProvider` parses the JWT `lang` claim from the raw `Authorization: Bearer` header (runs before `UseAuthentication`, so `context.User` is empty). Falls through to `Accept-Language` for unauthenticated endpoints. Supported: `en` (default), `it`.
- **i18n — email templates are file-based** — `SplitDuo.Core/EmailTemplates/{lang}/{TemplateKey}.html` (embedded resources), 11 templates × 2 languages. Subject stored as `<!-- SUBJECT: ... -->` HTML comment on line 1. Placeholders use `{{PropertyName}}` mustache syntax matching `ITemplateModel` properties; values are HTML-escaped. `IEmailTemplateProvider.Render(model, language)` falls back to `en` if the requested language file is missing. Pass the recipient's `UiLanguage` for authenticated users, `"en"` for new-user invitations.
- **i18n — supported languages are centralized** — `SplitDuo.Core/Localization/SupportedLanguages` is the single source of truth. Use `SupportedLanguages.IsSupported()` for validation, `SupportedLanguages.Normalize()` for silent fallback, `SupportedLanguages.Cultures` for culture registration, and `SupportedLanguages.Default` for the default. Adding a language is a one-line edit to `SupportedLanguages.All` + translation files. Never hardcode `"en" or "it"` allowlists.