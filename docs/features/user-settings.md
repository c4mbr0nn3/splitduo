# User Settings System

| Field | Value |
|---|---|
| Status | Approved — ready for implementation |
| Version | 1.0 |
| Date | 2026-07-25 |
| Owner | Product |
| Depends on | Existing auth flow (`AuthResponseDto.User`), `UsersController`, `useColorMode()` |

## Executive Summary

Introduce a per-user settings store so that a user's UI preferences persist server-side and follow them across devices on every login. The first setting is **theme** (`light` / `dark` / `auto`). The schema is designed to also carry **UI language** (`uiLanguage`) as a defaulted field, ready for a planned i18n upgrade that requires no migration. Settings are stored as a single `jsonb` column on the `users` table, mapped via EF Core 10 complex types (`ComplexProperty().ToJson()`), and embedded in the existing `UserDto` so login, token refresh, and `GET /users/me` all carry settings with zero extra round-trips. A dedicated `PUT /api/v1/users/me/settings` endpoint handles updates. On the frontend, a `useUserSettings` composable syncs the saved theme into `useColorMode()` on any auth state change, and a "Preferences" card on the profile page exposes the theme selector.

## Goals & Non-Goals

### Goals
- Persist the user's theme choice server-side so it applies on every login regardless of device.
- Make the settings shape trivially extensible: adding a new setting is a C# property addition, not a migration.
- Carry settings in the existing login/refresh/`/users/me` payloads — no new fetch on app start.
- Provide a `PUT /users/me/settings` endpoint for updates with validation.
- Apply the saved theme to the Nuxt UI color mode on login and on change.
- Expose a theme selector in the profile page.

### Non-Goals (v1)
- UI language selector in the UI — `uiLanguage` is schema-ready and defaults to `en`, but no selector ships in v1. i18n is a planned future upgrade.
- Multi-value or per-group settings — this is a single per-user global settings blob.
- Server-side querying/filtering of users by setting values.
- Importing the user's current `localStorage` theme into the server on first login (deferred; see Open Decisions).
- Explicit optimistic-concurrency conflict detection (no `[Timestamp]` token on `User`).

## Background & Context

Today the app has no settings infrastructure:
- `sd-backend/SplitDuo.Core/Domain/Entities/User.cs` has no settings/preferences/json column.
- `sd-backend/SplitDuo.Core/Persistence/AppContext.cs` has no `OnModelCreating` override — all entity config is data-annotation-only.
- The only `jsonb` precedent is `Import.cs` (`analysis_results`, `mapping_configuration`), mapped via `[Column(TypeName = "jsonb")]` + `string` + manual `JsonSerializer` helpers (the pre-EF8 pattern).
- `sd-frontend/app/components/button/ColorMode.vue` toggles `useColorMode().preference` between `dark`/`light`, stored in `localStorage` only — no server persistence, so theme resets per device.
- `UserDto` is embedded in `AuthResponseDto` and returned by login, 2FA-complete, refresh, and `GET /users/me`. Adding a field to `UserDto` automatically flows to all these responses.

The stack is .NET 10 + EF Core 10 + Npgsql 10 + Nuxt 4 SPA (`ssr: false`). EF Core 10 complex types (`ComplexProperty().ToJson()`) are the Npgsql-recommended modern path for strongly-typed JSON mapping (Npgsql 10.0 GA, Nov 22 2025).

## Architecture Decision

**Chosen: Single `jsonb` column on `users` mapped via EF Core 10 complex types.**

### Why this over the alternatives

| Alternative | Verdict | Reason |
|---|---|---|
| **Complex types (`ComplexProperty().ToJson()`)** | **Chosen** | Modern EF10 best practice; per-field change tracking; LINQ-into-JSON; additive evolution without migrations; value semantics. |
| `[Column(TypeName="jsonb")]` + `string` + manual `JsonSerializer` (matches `Import`) | Rejected | Whole-blob rewrite on every UPDATE → last-write-wins concurrency hazard for a cross-device sync feature (on the critical path, not theoretical). No per-field tracking. |
| Separate `user_settings` table (1:1, typed columns) | Rejected | A migration per new setting — directly contradicts "future-proof without rewriting." JOIN/`Include` on every login. Overkill for never-queried prefs. |
| Embed settings in JWT claims | Rejected | Solves a transport problem the embedded-`UserDto` already solves, while adding JWT bloat and staleness. Not a persistence solution. |

### Tensions resolved
- **Modern best practice vs matches existing codebase:** The `Import` string+manual-JSON pattern suits cold, write-once metadata. User settings are hot, cross-device, and evolving — a different use case justifies a different pattern. A `sd-backend/CLAUDE.md` rule will document when to use each.
- **First `OnModelCreating` override:** Worth it. Six lines unlock `ToJson()` + defaults no annotation offers. The annotation-only convention was emergent, not a stated rule.
- **Embed in `UserDto` vs separate endpoint:** Both. Embed for reads (zero round-trips on login/refresh/reload); dedicated `PUT /users/me/settings` for writes (don't bloat `UpdateUserRequestDto` with mixed concerns).
- **Version the JSON?** No. Preferences are additive — System.Text.Json leaves unknown keys alone and uses CLR defaults for missing properties. A `Version` field is only needed for breaking renames, which are unlikely for prefs.

## User Stories

### US1 — First login on a new device
> As a user, when I log in on a device I've never used, I want my saved theme to apply automatically without me touching anything.

**Acceptance:** After login, the app's color mode matches the `theme` value returned in the login response's `UserDto.Settings`.

### US2 — Change theme on one device, see it on another
> As a user, when I switch to dark mode on my phone, I want my laptop to be in dark mode next time I open it.

**Acceptance:** Toggling theme on device A sends `PUT /users/me/settings` with the new theme. On device B's next login or token refresh, the updated theme is returned and applied.

### US3 — Pick a theme from the profile page
> As a user, I want to choose my theme (light / dark / auto) from my profile settings.

**Acceptance:** The profile page has a "Preferences" card with a theme selector. Selecting an option immediately applies it and persists it server-side.

### US4 — New user gets a sensible default
> As a new user, I want a reasonable default theme without configuring anything.

**Acceptance:** A brand-new user's settings default to `theme = "auto"` (follows OS preference). Existing users backfilled by the migration also get `auto` on next login.

### US5 — Future: add a setting without a rewrite
> As a developer, I want to add a new setting (e.g. UI language) later without rewriting the settings system.

**Acceptance:** Adding a setting = add a property with a default initializer to the `UserSettings` POCO. No migration, no DTO rewrite, no endpoint change. Existing rows get the default on read.

## User Flows

### Flow 1 — Login applies saved theme
```
User submits credentials
  → POST /api/v1/auth/login
  → Backend returns AuthResponseDto { token, refreshToken, user: { ..., settings: { theme, uiLanguage } } }
  → Frontend: useAuth sets user.value = response.data.user
  → auth.client.js watch(user) fires → useUserSettings.syncFromUser(user)
  → syncFromUser sets colorMode.preference = user.settings.theme (mapped: "auto" → "system")
  → UI renders in saved theme
```

### Flow 2 — Toggle theme via header button
```
User clicks ColorMode button
  → colorMode.preference toggles instantly (local UI feedback)
  → useUserSettings.update({ theme: newTheme }) called
  → debounced (400ms) PUT /api/v1/users/me/settings { theme: newTheme }
  → Backend validates, updates user.Settings.Theme, SaveChangesAsync
  → Returns updated UserSettingsDto
  → settings state updated
```

### Flow 3 — Pick theme from profile page
```
User opens /profile
  → Preferences card shows current theme (light/dark/auto)
  → User selects "dark"
  → useUserSettings.update({ theme: "dark" })
  → same debounced PUT as Flow 2
  → colorMode.preference = "dark" applied immediately
```

### Flow 4 — App reload restores theme
```
User reloads the SPA
  → auth.client.js initialize() → GET /api/v1/users/me
  → user.value set from response (includes settings)
  → watch(user, { immediate: true }) fires → syncFromUser applies theme
```

### Flow 5 — Token refresh carries settings
```
JWT nears expiry → proactive refresh
  → POST /api/v1/auth/refresh
  → Backend returns new AuthResponseDto with fresh user.settings
  → user.value updated → watch fires → theme re-synced
```

## Technical Design

### Data Model

#### New POCO: `UserSettings`

**File:** `sd-backend/SplitDuo.Core/Domain/Entities/UserSettings.cs` (new)

```csharp
namespace SplitDuo.Core.Domain.Entities;

/// <summary>
/// Per-user UI preferences. Stored as jsonb on the users table.
/// Add new settings here with a default initializer — no migration needed
/// for additive changes (System.Text.Json uses CLR defaults for missing keys).
/// </summary>
public class UserSettings
{
    /// <summary>"light" | "dark" | "auto" (follows OS)</summary>
    public string Theme { get; set; } = "auto";

    /// <summary>ISO 639-1 code. Only "en" accepted in v1; widened when i18n lands.</summary>
    public string UiLanguage { get; set; } = "en";
}
```

Mutable class (not record — `init` setters cause EF materialization friction). Non-null defaults on every property for forward-compat.

#### Entity update: `User`

**File:** `sd-backend/SplitDuo.Core/Domain/Entities/User.cs`

Add:
```csharp
public UserSettings Settings { get; set; } = new();
```

#### DbContext: first `OnModelCreating` override

**File:** `sd-backend/SplitDuo.Core/Persistence/AppContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>(e =>
    {
        e.ComplexProperty(u => u.Settings, s => s.ToJson("settings"));
    });
}
```

Explicit column name `"settings"` (snake_case) to match the rest of the schema — EF defaults to PascalCase `"Settings"`.

#### Migration

**Command:**
```bash
dotnet ef migrations add AddUserSettings --project SplitDuo.Core --startup-project SplitDuo.Api
```

**Expected SQL:**
```sql
ALTER TABLE users ADD settings jsonb NOT NULL DEFAULT '{}'::jsonb;
```

Existing rows backfill to `'{}'`; C# property initializers supply `Theme="auto"`, `UiLanguage="en"` on read. Auto-applies on next app startup via `Database.MigrateAsync()`.

### Backend API

#### Endpoint: Get current user settings

No new GET endpoint — settings ride the existing `UserDto` returned by:
- `POST /api/v1/auth/login`
- `POST /api/v1/auth/verify-2fa`
- `POST /api/v1/auth/refresh`
- `GET /api/v1/users/me`

#### Endpoint: Update current user settings

```
PUT /api/v1/users/me/settings
Authorization: Bearer <jwt>
Content-Type: application/json

Request body:
{
  "theme": "dark",          // optional, nullable = leave unchanged
  "uiLanguage": "en"        // optional, nullable = leave unchanged
}

200 OK:
{
  "success": true,
  "data": { "theme": "dark", "uiLanguage": "en" },
  "message": "Settings updated successfully"
}

400 Bad Request (validation):
{
  "success": false,
  "errors": { "theme": ["Theme must be 'light', 'dark', or 'auto'"] }
}

401 Unauthorized
```

Full-object PUT semantics with nullable fields meaning "leave unchanged" (matches existing `UpdateUserRequestDto` convention). The frontend always sends the full settings object it has, so in practice all fields are present.

#### DTOs

**File:** `sd-backend/SplitDuo.Api/Features/Users/Dto/UserSettingsDto.cs` (new)

```csharp
public class UserSettingsDto
{
    public string Theme { get; set; } = "auto";
    public string UiLanguage { get; set; } = "en";

    public UserSettingsDto() { }
    public UserSettingsDto(UserSettings settings)
    {
        Theme = settings.Theme;
        UiLanguage = settings.UiLanguage;
    }
}
```

**File:** `sd-backend/SplitDuo.Api/Features/Users/Dto/UpdateUserSettingsRequestDto.cs` (new)

```csharp
public class UpdateUserSettingsRequestDto
{
    [RegularExpression("^(light|dark|auto)$",
        ErrorMessage = "Theme must be 'light', 'dark', or 'auto'")]
    public string? Theme { get; set; }

    [RegularExpression("^en$",
        ErrorMessage = "Unsupported language")]
    public string? UiLanguage { get; set; }
}
```

`UiLanguage` validation accepts only `en` in v1. When i18n lands, widen the regex (or switch to a custom attribute backed by a supported-locales list). No migration, no DTO shape change.

**File:** `sd-backend/SplitDuo.Api/Features/Users/Dto/UserDto.cs` — add:

```csharp
public UserSettingsDto Settings { get; set; } = new();
// in UserDto(User user) constructor:
Settings = new UserSettingsDto(user.Settings);
```

#### Service layer

**Files:** `sd-backend/SplitDuo.Api/Features/Users/Services/IUsersService.cs` + `UsersService.cs`

Add:
```csharp
Task<Result<UserSettingsDto>> UpdateCurrentUserSettingsAsync(
    Guid userGuid, UpdateUserSettingsRequestDto request);
```

Implementation: load user via `GetCurrentUserAsync` pattern, apply non-null fields from the request to `user.Settings`, return `new UserSettingsDto(user.Settings)`. Controller calls `unitOfWork.SaveChangesAsync()` on success (matches existing controller-save pattern).

#### Controller

**File:** `sd-backend/SplitDuo.Api/Features/Users/Controllers/UsersController.cs`

Add `PUT /me/settings` endpoint following the existing `PUT /me` pattern: get current user id → call service → `SaveChangesAsync` on success → `HandleResult`.

### Frontend

#### Composable: `useUserSettings`

**File:** `sd-frontend/app/composables/resources/useUserSettings.js` (new)

```javascript
export default function useUserSettings() {
  const api = useApi()
  const colorMode = useColorMode()
  const settings = useState('user-settings', () => ({
    theme: 'auto',
    uiLanguage: 'en',
  }))

  const applyTheme = (theme) => {
    // "auto" maps to Nuxt UI's "system" preference
    colorMode.preference = theme === 'auto' ? 'system' : theme
  }

  // Called on any auth state change (login, refresh, reload)
  const syncFromUser = (user) => {
    if (user?.settings) {
      settings.value = user.settings
      applyTheme(user.settings.theme)
    }
  }

  // Debounced PUT (400ms) — avoids request storms on rapid toggles
  const update = useDebounceFn(async (patch) => {
    const res = await api.put('/users/me/settings', patch)
    if (res.success && res.data) {
      settings.value = res.data
      applyTheme(res.data.theme)
    }
    return res
  }, 400)

  return {
    settings: readonly(settings),
    syncFromUser,
    update,
  }
}
```

Uses `useDebounceFn` from `@vueuse/core` (already available — see existing `useDebounceSearch`).

#### Plugin wiring: single sync point

**File:** `sd-frontend/app/plugins/auth.client.js`

Add a `watch` on the `user` state so any auth state change (login, refresh, reload restore) applies the saved theme in one place — no need to edit `useAuth.js` in 4 places:

```javascript
const { syncFromUser } = useUserSettings()
const { user } = useAuth()
watch(user, (u) => { if (u) syncFromUser(u) }, { immediate: true })
```

#### ColorMode button integration

**File:** `sd-frontend/app/components/button/ColorMode.vue`

On toggle, call `useUserSettings().update({ theme: newTheme })` in addition to setting `colorMode.preference`. Local toggle stays instant; the debounced PUT syncs server-side.

#### Profile page: Preferences card

**File:** `sd-frontend/app/pages/profile.vue`

Add a "Preferences" card with a theme selector (light / dark / auto). On change, call `useUserSettings().update({ theme: value })`. No language selector in v1 (`uiLanguage` is schema-ready but has no UI yet).

## Schema Evolution / Future-Proofing Strategy

### Adding a new setting (the common case)
1. Add a property with a default initializer to `UserSettings`.
2. Add it to `UserSettingsDto` and `UpdateUserSettingsRequestDto` (with validation).
3. Add UI if applicable.

**No migration.** Existing rows' JSON lacks the new key → System.Text.Json leaves the property at its CLR default (set by the initializer). New users get the default; existing users get it on next read.

### Widening an accepted value set (e.g. adding languages)
Widen the `[RegularExpression]` on the request DTO, or replace with a custom validation attribute backed by a supported-locales list. No schema change.

### Breaking change (rename/remove a field) — unlikely for prefs
This is the only case that requires a migration + data backfill. At that point, add a `Version` field to `UserSettings` and a one-time migration step. Not needed for additive evolution.

## Acceptance Criteria

### Backend
- [ ] `UserSettings` POCO exists with `Theme` (default `"auto"`) and `UiLanguage` (default `"en"`).
- [ ] `User.Settings` property added; `AppContext.OnModelCreating` maps it via `ComplexProperty().ToJson("settings")`.
- [ ] Migration `AddUserSettings` generates `ALTER TABLE users ADD settings jsonb NOT NULL DEFAULT '{}'::jsonb`.
- [ ] Migration applies cleanly on startup against an existing database (existing rows backfilled to `'{}'`).
- [ ] `UserDto` includes `Settings`; `UserDto(User)` constructor maps it.
- [ ] Login, 2FA-complete, refresh, and `GET /users/me` responses all include `settings`.
- [ ] `PUT /api/v1/users/me/settings` updates non-null fields, validates via DataAnnotations, returns updated `UserSettingsDto`.
- [ ] Invalid `theme` (not light/dark/auto) → 400 with validation error.
- [ ] Invalid `uiLanguage` (not `en`) → 400 with validation error.
- [ ] Unauthenticated request → 401.

### Frontend
- [ ] `useUserSettings` composable exists with `settings` (readonly), `syncFromUser`, `update`.
- [ ] `auth.client.js` watches `user` and calls `syncFromUser` on any change (immediate).
- [ ] After login, the app's color mode matches `user.settings.theme` (`auto` → system).
- [ ] Toggling `ColorMode.vue` calls `update({ theme })` — debounced PUT fires within 400ms.
- [ ] Profile page has a "Preferences" card with a theme selector (light/dark/auto).
- [ ] Selecting a theme on the profile page applies it immediately and persists server-side.
- [ ] Reloading the SPA restores the saved theme.
- [ ] `pnpm lint:fix` passes.

### Cross-device sync
- [ ] Change theme on device A → `PUT` succeeds → log in on device B → device B renders the updated theme.

## Implementation Plan (Change Set)

Ordered by dependency. Backend and frontend can be built in parallel once the DTO shape is agreed.

### Backend
1. `sd-backend/SplitDuo.Core/Domain/Entities/UserSettings.cs` — new POCO.
2. `sd-backend/SplitDuo.Core/Domain/Entities/User.cs` — add `Settings` property.
3. `sd-backend/SplitDuo.Core/Persistence/AppContext.cs` — add `OnModelCreating` with `ComplexProperty(...).ToJson("settings")`.
4. Generate migration: `dotnet ef migrations add AddUserSettings --project SplitDuo.Core --startup-project SplitDuo.Api`. Verify generated column is `settings jsonb NOT NULL DEFAULT '{}'::jsonb`.
5. `sd-backend/SplitDuo.Api/Features/Users/Dto/UserSettingsDto.cs` — new.
6. `sd-backend/SplitDuo.Api/Features/Users/Dto/UserDto.cs` — add `Settings` + map in constructor.
7. `sd-backend/SplitDuo.Api/Features/Users/Dto/UpdateUserSettingsRequestDto.cs` — new, nullable fields + `[RegularExpression]`.
8. `sd-backend/SplitDuo.Api/Features/Users/Services/IUsersService.cs` + `UsersService.cs` — `UpdateCurrentUserSettingsAsync`.
9. `sd-backend/SplitDuo.Api/Features/Users/Controllers/UsersController.cs` — `PUT /me/settings`.

### Frontend
10. `sd-frontend/app/composables/resources/useUserSettings.js` — new composable.
11. `sd-frontend/app/plugins/auth.client.js` — `watch(user, syncFromUser, { immediate: true })`.
12. `sd-frontend/app/components/button/ColorMode.vue` — call `update({ theme })` on toggle.
13. `sd-frontend/app/pages/profile.vue` — add "Preferences" card with theme selector.

### Verification
14. Manual: create user → login → toggle theme → reload → verify persistence. Change on one browser, log in on another → verify sync.
15. `pnpm lint:fix` in `sd-frontend`.
16. Optional: integration test in `SplitDuo.Tests.Integration` (project exists, no tests yet — this would be the first).

## Testing Strategy

### Manual verification (minimum)
1. Start backend (`dotnet run --project SplitDuo.Api`) + frontend (`pnpm dev`).
2. Log in as `admin@localhost` / `changeme`.
3. Confirm default theme `auto` applies (matches OS).
4. Toggle to `dark` via header button → reload → confirm `dark` persists.
5. Open profile page → select `light` → confirm immediate apply + persisted after reload.
6. Open a different browser → log in → confirm theme matches the last saved value.
7. Inspect DB: `SELECT settings FROM users WHERE email = 'admin@localhost';` → confirm `{"Theme":"dark","UiLanguage":"en"}` (or camelCase depending on serializer config — verify and align with frontend expectations).

### Serializer case alignment (verify during implementation)
System.Text.Json defaults to PascalCase property names in serialized JSON unless configured. Confirm whether the JSON column stores `{"Theme":...}` or `{"theme":...}` and ensure the frontend reads it consistently. If the frontend expects camelCase, configure `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` on the complex property, or map to camelCase in the DTO (the DTO is what the frontend sees, so DTO casing is what matters for the API contract — the internal JSON column casing is transparent).

### Automated (optional, recommended)
An integration test in `SplitDuo.Tests.Integration` that:
1. Creates a user.
2. `PUT /users/me/settings` with `{ theme: "dark" }`.
3. `GET /users/me` → asserts `settings.theme == "dark"`.
4. Reloads the user entity from DB → asserts `Settings.Theme == "dark"` (round-trip through jsonb).

This would be the first test in the project — bootstrap the test harness only if the manual verification leaves uncertainty.

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| EF10 complex types are new (Nov 2025) — undiscovered bugs | Low | Medium | Npgsql 10 GA + docs recommend it. One round-trip integration test catches regressions. Fallback is Alt 2 (string + manual JSON). |
| Column named `Settings` (PascalCase) instead of `settings` (snake_case) | Medium | Low | Explicit `ToJson("settings")` in Fluent config. Verify in generated migration SQL. |
| Serializer casing mismatch (PascalCase JSON vs camelCase frontend) | Medium | Low | Verify during implementation; configure `JsonNamingPolicy.CamelCase` if needed, or rely on DTO casing for the API contract. |
| Existing users surprised by theme change on backfill | Low | Low | Backfill is `auto` (follows OS) — most users see no change. Acceptable per decision. |
| Two devices update different fields simultaneously | Low | Low | Alt 1's per-field change tracking means different fields don't clobber within the JSON. Same-field concurrent writes are last-write-wins (no `[Timestamp]` token on `User`) — acceptable for prefs. |
| `OnModelCreating` override sets precedent for inconsistent Fluent/annotation mixing | Low | Low | Add a `sd-backend/CLAUDE.md` rule documenting when to use each pattern. |

## CLAUDE.md Updates (per repo rule #6)

After implementation, add:

### `sd-backend/CLAUDE.md`
> **JSON columns:** Use `ComplexProperty().ToJson()` (in `AppContext.OnModelCreating`) for hot/evolving per-entity blobs like user settings. Reserve the `[Column(TypeName = "jsonb")]` + manual `JsonSerializer` pattern (see `Import`) for cold, write-once metadata. Add new settings as properties with default initializers on the POCO — no migration needed for additive changes.

### `sd-frontend/CLAUDE.md`
> **User settings:** Live in `useState('user-settings')` via `useUserSettings` composable. `auth.client.js` watches `user` and calls `syncFromUser` to apply the saved theme on any auth state change. Updates go via debounced PUT to `/users/me/settings`. Theme `"auto"` maps to Nuxt UI's `"system"` color-mode preference.

### Root `CLAUDE.md`
No root-level guideline established — skip.

## Out of Scope / Future Work

- **UI language selector** — `uiLanguage` field is schema-ready (default `en`, validation `^en$`). When i18n lands: widen the regex, add a selector to the Preferences card, wire to `vue-i18n` (or equivalent). Zero migration.
- **Import existing `localStorage` theme** — on first login after this ships, a user's current `localStorage` theme could be uploaded as a one-time client-side migration. Deferred; the `auto` backfill is acceptable.
- **Optimistic concurrency token on `User`** — if settings conflicts become real, add a `[Timestamp]`/`xmin` token. Out of scope for v1.
- **Server-side settings queries** — e.g. "how many users use dark mode." Alt 1 supports LINQ-into-JSON (`u.Settings.Theme == "dark"`) if this is ever needed. No action now.
- **Per-group settings** — current design is a single global per-user blob. A separate group-settings system would be a new feature.

## Open Decisions Resolved

| # | Question | Decision |
|---|---|---|
| 1 | Default theme value + backfill behavior | `auto` for new users; existing rows backfilled to `'{}'` → C# defaults give `auto`/`en` on next login. |
| 2 | Embed settings in `UserDto` vs separate fetch | Embed in `UserDto` (zero extra round-trips on login/refresh/reload). Dedicated `PUT /users/me/settings` for updates. |
| 3 | Settings UI placement | "Preferences" card on the existing `profile.vue` (not a separate settings page). |
| 4 | Supported language list for validation | `en` only in v1 (`[RegularExpression("^en$")]`). `uiLanguage` field is schema-ready; widen regex when i18n lands. No language selector UI in v1. |
| 5 | Backfill behavior for existing users | Existing users silently get `auto`/`en` on next login. Confirmed acceptable. |
| 6 | Optimistic concurrency | Out of scope. `User` has no concurrency token; updates are last-write-wins at row level. Alt 1's per-field tracking prevents cross-field clobbering within the JSON. Sufficient for prefs. |