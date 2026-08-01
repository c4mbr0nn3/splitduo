# Guide: Adding a New Language to SplitDuo

High-level checklist for adding a new UI language (e.g. French `fr`, Spanish `es`).
The system is designed so that adding a language is a bounded, mostly-mechanical
task. The build-time locale key parity check and the centralized
`SupportedLanguages` constant are the two guardrails that catch mistakes.

## Overview

Adding a language touches three layers: **frontend strings**, **frontend wiring**,
and **backend wiring + translations**. Each layer has a single source of truth or
a guardrail that prevents silent partial-language bugs.

| Layer | Source of truth / guardrail | What you provide |
|---|---|---|
| Frontend strings | `i18n/locales/<code>.json` — parity checked at build time | One JSON file, full key parity with `en.json` |
| Frontend wiring | `nuxt.config.ts` locales array + `app.vue` Nuxt UI locale map + `LanguageSwitcher.vue` | Three 1-line edits |
| Backend wiring | `SupportedLanguages.All` in `SplitDuo.Core/Localization/SupportedLanguages.cs` | One set entry |
| Backend translations | `.resx` files per service + email templates | ~12 `.resx` files + 11 HTML email templates |

## Prerequisites

- The locale key parity check (`pnpm check:locales` from `sd-frontend/`) must pass
  before you start. It runs automatically on `pnpm dev` / `pnpm generate` via the
  `modules:done` hook in `nuxt.config.ts`.
- `dotnet build` (from `sd-backend/`) must pass.

## Step 1 — Frontend locale file

Create `sd-frontend/i18n/locales/<code>.json` by copying `en.json` and translating
every value. The key set must be **identical** to `en.json` — the build-time parity
check will fail the build if any key is missing or extra.

```
sd-frontend/i18n/locales/en.json   ← reference (source of truth for keys)
sd-frontend/i18n/locales/it.json
sd-frontend/i18n/locales/<code>.json   ← new file
```

Verify:

```sh
cd sd-frontend && pnpm check:locales
```

## Step 2 — Frontend wiring (3 edits)

### 2a. Register the locale in `nuxt.config.ts`

Add an entry to the `i18n.locales` array:

```ts
locales: [
  { code: 'en', file: 'en.json', name: 'English' },
  { code: 'it', file: 'it.json', name: 'Italiano' },
  { code: '<code>', file: '<code>.json', name: '<NativeName>' }, // new
],
```

### 2b. Add the Nuxt UI locale in `app/app.vue`

Nuxt UI ships its own locale packs. Import the new one and add it to the
`uiLocales` map:

```js
import { en, it, <code> } from '@nuxt/ui/locale'

const uiLocales = { en, it, <code> }
```

If Nuxt UI does not ship a locale pack for your language, the fallback
(`uiLocales[locale.value] ?? en`) serves English for Nuxt UI's own component
strings (calendar, picker, etc.). File an issue upstream or contribute the
locale pack to `@nuxt/ui`.

### 2c. Add the option to `LanguageSwitcher.vue`

Add an entry to the language options array in
`sd-frontend/app/components/settings/LanguageSwitcher.vue`:

```js
const languages = [
  { label: 'English', value: 'en' },
  { label: 'Italiano', value: 'it' },
  { label: '<NativeName>', value: '<code>' }, // new
]
```

The label is the language's native name (proper noun) — not translated via `$t()`.

## Step 3 — Backend wiring (1 edit)

Add the language code to `SupportedLanguages.All` in
`sd-backend/SplitDuo.Core/Localization/SupportedLanguages.cs`:

```csharp
public static readonly IReadOnlySet<string> All = new HashSet<string>(["en", "it", "<code>"]);
```

This single edit covers all 9 backend sites that previously hardcoded the
`"en" or "it"` allowlist: culture registration, JWT `lang` claim validation,
`UserContextService.GetCurrentUserLanguage()`, `UsersService` validation gate,
and email-language selection in `PasswordResetService`, `GroupsService`, and
`InvitationsService`. Fallback is always `SupportedLanguages.Default` (`"en"`).

Verify:

```sh
cd sd-backend && dotnet build && dotnet test SplitDuo.Tests.Unit
```

## Step 4 — Backend translations (`.resx`)

Create a `.<code>.resx` counterpart for **every** service that has an `.en.resx`.
The `.resx` files live alongside the services:

```
sd-backend/SplitDuo.Api/Resources/Features/<Slice>/Services/<Service>.en.resx
sd-backend/SplitDuo.Api/Resources/Features/<Slice>/Services/<Service>.it.resx
sd-backend/SplitDuo.Api/Resources/Features/<Slice>/Services/<Service>.<code>.resx  ← new
sd-backend/SplitDuo.Core/Resources/Services/<Service>.en.resx
sd-backend/SplitDuo.Core/Resources/Services/<Service>.it.resx
sd-backend/SplitDuo.Core/Resources/Services/<Service>.<code>.resx  ← new
```

The key set in each `.<code>.resx` must match the `.en.resx` counterpart.
ASP.NET `IStringLocalizer` falls back to the neutral `.resx` (which is `.en.resx`
in this project) for any missing key — this is a silent fallback, so be thorough.

> **Note:** `BaseApiController` currently only has `.en.resx` (the `.it.resx` is
> missing — a pre-existing gap). Add both `.<code>.resx` and `.it.resx` if you
> want full coverage.

## Step 5 — Email templates

Create `sd-backend/SplitDuo.Core/EmailTemplates/<code>/` and translate all 11
HTML files from the `en/` folder:

```
sd-backend/SplitDuo.Core/EmailTemplates/en/<TemplateKey>.html
sd-backend/SplitDuo.Core/EmailTemplates/it/<TemplateKey>.html
sd-backend/SplitDuo.Core/EmailTemplates/<code>/<TemplateKey>.html  ← new
```

`EmailTemplateProvider` falls back to `en` if a language folder is missing —
so this step is safe to defer, but users will receive English emails until
the templates are added.

## Step 6 — Verify end-to-end

```sh
# Frontend
cd sd-frontend
pnpm check:locales          # parity check
pnpm lint:fix               # lint
pnpm dev                    # start dev server, switch language in UI, verify:
                            #   - currency/dates format correctly
                            #   - all strings translated
                            #   - no console errors

# Backend
cd sd-backend
dotnet build
dotnet test SplitDuo.Tests.Unit
./run-integration-tests.sh  # full integration suite (Testcontainers)
```

Manual checks after language switch in the UI:
- Currency amounts use the locale's separators and symbol position
- Dates use the locale's month names and ordering
- DatePicker label renders in the locale's date style
- Chart axis labels and title are translated
- Error messages from the backend are translated
- Email notifications arrive in the selected language

## Future features: adding language-specific files

When a future feature introduces a new kind of localized artifact, add a
section here documenting where the file goes and what the guardrail is.
The pattern to follow:

1. **One file per language**, mirroring the `en` reference.
2. **A guardrail that catches drift** — either a build-time check (like the
   locale JSON parity check) or a graceful fallback (like
   `EmailTemplateProvider` falling back to `en`).
3. **No scattered allowlists** — the supported-language set comes from
   `SupportedLanguages.All`, not from per-feature hardcoded checks.

### Checklist for new feature authors

If your feature adds user-facing strings or localized content, ask:

- [ ] Does it add keys to `i18n/locales/*.json`? If yes, add to all locale files
      — the parity check will catch omissions.
- [ ] Does it add backend user-facing strings? If yes, add `.resx` entries to
      every language's `.resx` for that service.
- [ ] Does it add email templates? If yes, add to every `EmailTemplates/<code>/`
      folder (or accept `en` fallback).
- [ ] Does it introduce a new "supported languages" check? If yes, use
      `SupportedLanguages.IsSupported()` / `Normalize()` — do not hardcode a
      new allowlist.
- [ ] Does it use locale-aware formatting? If yes, read the active locale from
      `useNuxtApp().$i18n.locale.value` (utilities) or `useI18n().locale`
      (components/composables) — do not hardcode a locale string.

## Reference

- **Conventions:** `sd-frontend/CLAUDE.md` and `sd-backend/CLAUDE.md` — i18n rules
  sections