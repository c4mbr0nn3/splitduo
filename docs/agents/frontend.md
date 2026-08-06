# Frontend Guide — sd-frontend

> Read this before modifying `sd-frontend/`. Canonical source for frontend conventions, patterns, and rules.

## Overview

Nuxt 4 SPA (ssr: false), Vue 3 Composition API, Nuxt UI v4, TailwindCSS v4. No Pinia.

## Commands

- `pnpm dev` — http://localhost:3000
- `pnpm generate` — static build → `.output/public/` (deployed into backend wwwroot)
- `pnpm lint:fix` — **run after every implementation** before considering task done
- `pnpm typecheck` — **run after every implementation** before considering task done (`nuxt prepare && vue-tsc --noEmit`)
- `pnpm gen:api` — regenerate `app/types/api.d.ts` from `docs/api/splitduoapi-v1.yaml` (run after OpenAPI spec changes)

## Tech Stack

- Nuxt 4 (`^4.4.8`), Nuxt UI v4 (`^4.9.0`), TailwindCSS v4, **TypeScript** (`strict: true`, `verbatimModuleSyntax: true`)
- Icons: Lucide + Simple Icons (`@iconify-json/*`)
- ApexCharts (`apexcharts` + `vue3-apexcharts`) — group statistics charting. Tree-shaken via `vue3-apexcharts/core` in `plugins/apexcharts.client.ts`: only `bar` + `donut` chart types and `legend` + `toolbar` features are imported. Add new chart types/features there if a chart needs them.
- PWA (`@vite-pwa/nuxt`) — installable app, offline support, auto-update
- uqr — QR code generation for 2FA
- `openapi-typescript` — generates `app/types/api.d.ts` from OpenAPI spec (dev-only, zero runtime cost)
- ESLint (`@nuxt/eslint` with stylistic rules, includes `@typescript-eslint`)

## Structure

- `app/types/` — `api.d.ts` (generated from OpenAPI spec via `pnpm gen:api`), `domain.ts` (friendlier aliases + envelope types + alias-mode unions + hand-write exceptions for `ImportAnalysis`/`KeyValue`/`BalanceSummary`/`BalanceSuggestion`)
- `app/components/` — auto-imported, folder nesting = name prefix (`groups/ExpenseCard.vue` → `<GroupsExpenseCard />`). Subfolders: `admin/`, `button/`, `dashboard/`, `expenses/`, `groups/` (+ `members/`), `layout/`, `ui/` (primitives)
- `app/composables/` — `api/base.ts` (useApi, incl. `getBlob` for binary downloads), `auth/` (useAuth, useAuthToken, use2FA), `resources/` (one per entity, incl. `useAliases`), `ui/` (useModal, useChartTheme), `utils/` (useNotifications, useErrorHandling, usePagination, useDebounceSearch), `index.ts` (barrel)
- `app/pages/` — file-based routing, nested folders = nested routes
- `app/middleware/` — `auth.ts` (redirect to / if not authenticated), `admin.ts` (redirect to /dashboard if not admin)
- `app/plugins/` — `auth.client.ts` (restores auth state on app start), `auth-refresh.client.ts` (proactive token refresh: timer + visibilitychange), `apexcharts.client.ts` (registers ApexCharts globally)
- `app/utils/` — currency, date, enumUtils, jwt, userRoles, withMinDuration

## API Layer

`useApi()` (`composables/api/base.ts`) wraps `$fetch` with auth headers. Dev: `http://localhost:8080/api/v1`, prod: `/api/v1`. Methods: `get<T>()`, `getPaginated<T>()`, `post<T>()`, `put<T>()`, `delete()`, `getBlob()`. **Never call `$fetch` directly** — always use `useApi()`. Two distinct envelope types: `ApiEnvelope<T>` (non-paginated) vs `PaginatedEnvelope<T>` (paginated) — use `getPaginated<T>()` for paginated endpoints.

Backend response shape: `{ success, data, error, pagination }` — see `docs/agents/backend.md` for details. Types generated from OpenAPI spec in `app/types/api.d.ts`.

## Composable Patterns

**Resource composables** (`resources/`): one per entity. Internal state via `ref()`, exposed via `readonly()`. `isLoading` on every async op. Errors caught internally → `useNotifications().showError()`. `useExpenses(groupId)` accepts reactive params via `toRef()`.

**Singleton composables** (`useCategories`, `usePaymentModes`): global `ref()` declared outside function — shared across callers, auto-fetches on first use.

**Auth**: `useAuthToken` (token in `useState` bridged to `useCookie` for persistence — synchronous shared source of truth; the 7d cookie `maxAge` is intentional because `/auth/refresh` needs the expired JWT), `useAuth` (login/logout/refresh, user in cookie + `useState('user')`, exposes `isAuthenticated`/`isGlobalAdmin`), `use2FA` (TOTP/backup/email flow). Proactive refresh via `plugins/auth-refresh.client.ts` (timer + `visibilitychange`); `utils/jwt.ts` decodes `exp` for scheduling.

**Utility**: `useNotifications` (toast wrapper), `useErrorHandling`, `usePagination` (factory), `useDebounceSearch` (@vueuse/core), `useModal` (Nuxt UI useOverlay, returns Promise<boolean>), `useChartTheme`, `useSmartBack(parentRoute)` (smart back nav — `router.back()` if in-app history exists, else `navigateTo(parentRoute, { replace: true })`; powers `UiCardHeader`'s `backTo` prop and page-level cancel handlers).

## Component Conventions

- `<script setup lang="ts">` only. Props via type-based `defineProps<Props>()` + `withDefaults` (not runtime validators). Emits via `defineEmits<{ 'event': [args] }>()`. Model via `defineModel<Type>({ ... })`.
- Auto-imported: folder nesting = name prefix (`groups/ExpenseCard.vue` → `<GroupsExpenseCard />`)
- Use Nuxt UI components (`UButton`, `UInput`, `UCard`, `UModal`) — don't build custom equivalents
- Forms emit `@submit` and `@cancel`
- Extract into components when template chunk has clear responsibility or repeats. Primitives → `components/ui/`, domain → feature folder

## TypeScript

The frontend is fully TypeScript — no `.js`/`.jsx` source files. `tsconfig` (Nuxt-generated) is `strict: true`, `verbatimModuleSyntax: true`, `noUncheckedIndexedAccess: true`. ESLint enforces `@typescript-eslint/no-explicit-any` as an error.

- **`<script setup lang="ts">` only** for new `.vue` files — no plain `<script setup>`
- **Type-based macros** — `defineProps<Props>()` + `withDefaults`, `defineEmits<{...}>()`, `defineModel<T>()` (not runtime validator objects)
- **`import type`** for type-only imports (`verbatimModuleSyntax: true` is on) — `import type { Foo }` or `import { type Foo }`
- **No `any` — avoid it wherever possible.** ESLint `@typescript-eslint/no-explicit-any` is on (error). Prefer, in order:
  - `unknown` + narrowing (`typeof`, `in`, `instanceof`) for opaque/untrusted values
  - generics for reusable, type-preserving code
  - explicit interfaces/types for known shapes
  - `Record<string, unknown>` for bag-of-props, not `Record<string, any>`
  - `as const` / `satisfies` for literal inference
  - Only as a last resort, with a `// eslint-disable-next-line @typescript-eslint/no-explicit-any -- <reason>` comment explaining why no alternative works
- **No `as unknown as X` double-casts** unless bridging a genuinely untyped boundary (e.g. generated types, `$fetch` internals) — and add a one-line comment. Prefer fixing the source type or using a typed helper.
- **No `@ts-ignore`** — use `@ts-expect-error` with a reason comment if a suppression is truly unavoidable (it errors out once the underlying type is fixed, unlike `@ts-ignore` which silently rots).
- **No non-null assertion (`!`)** on values that can legitimately be `undefined`/`null` — narrow with `if (x)` / `??` / optional chaining instead. `!` is acceptable only on framework invariants that cannot be typed (e.g. a Nuxt auto-import known to exist), with a comment.
- **Error handling** — `catch (error)` blocks use `unknown` + narrowing (`typeof error === 'object' && 'statusCode' in error`). Never `catch (error: any)`.
- **Domain types** in `app/types/domain.ts` (re-exported from generated `app/types/api.d.ts` with friendlier names + `WithRequired` narrowing). Generated types are the source of truth — regenerate with `pnpm gen:api` after OpenAPI spec changes. Do NOT edit `api.d.ts` by hand
- **API layer** — `useApi().get<T>()` for non-paginated, `useApi().getPaginated<T>()` for paginated endpoints. Two distinct envelope types (`ApiEnvelope<T>` vs `PaginatedEnvelope<T>`) — never conflate
- **`useState`/`useCookie`** need explicit generics when the initial value is `null` — `useState<User | null>('user', () => null)`, `useCookie<string | null>('auth-token', ...)`
- **Run `pnpm typecheck`** before considering any frontend task done (in addition to `pnpm lint:fix`)

## Styling

- TailwindCSS v4 via Nuxt UI. Theme: primary=teal, secondary=rose, neutral=zinc (`app.config.ts`). Font: Geist (`main.css`).
- Dark mode via Nuxt UI color mode (`<UColorModeButton />`)
- No scoped styles, no CSS modules — utility classes only
- **Mobile first**: design small screens first, enhance with `sm:`/`md:`/`lg:`. Never desktop-first.

## UI Design Rules

- **Page vertical rhythm**: default pages use `py-6 px-4 sm:py-8`; auth pages are centered with `p-4` and no `py-*` page wrapper.
- **Auth page layout**: `min-h-dvh flex items-center justify-center p-4`; card is `UCard class="w-full max-w-md"`.
- **Page headers**: use `UiCardHeader`. Page title: `text-2xl font-bold text-primary`; card title: `text-lg font-semibold`.
- **Buttons**:
  - Primary CTA: default `UButton` (solid primary).
  - Destructive CTA: `color="error"`.
  - Secondary/neutral: `variant="outline"` or `variant="ghost"` with `color="neutral"`.
  - Icon-only actions: `size="sm" square`; minimum adjacent gap `gap-2`, prefer `gap-3` near destructive actions.
- **Form containers**: complex forms `UCard class="w-full max-w-2xl"`; auth forms `UCard class="w-full max-w-md"`.
- **Semantic colors only**: use Nuxt UI tokens (`success`, `error`, `warning`, `info`, `primary`, `secondary`, `neutral`). Never use raw `text-green-600`, `text-red-600`, `bg-green-100`, `border-gray-*`, etc.
- **Icons**: Lucide only (`i-lucide-*`). No Heroicons unless a Lucide equivalent is truly missing.
- **Search input**: list-page search uses `class="w-full sm:w-64 md:w-80"`.
- **Empty states**: `UiEmptyState` is used bare; if inside a card, the card uses `variant="ghost"` or `variant="soft"`, never `variant="outline"`.
- **Toasts**: `useNotifications` passes `duration: 4000` and `position: 'top-center'` to every toast.
- **Page loading/error**: use `UiLoadingSpinner` for loading and `UiEmptyState` + retry `UButton` for fetch failures.
- **Safe-area**: default layout main content adds `pb-[env(safe-area-inset-bottom)]`; PWA update button clears safe-area insets.
- **No global FAB**: navigation stays in `UHeader` drawer; keep existing group-page add-expense button.

## State

No Pinia. State via: composable-local `ref()` → `readonly()`, singleton refs (outside function), `useState()` (auth user), `useCookie()` (tokens, persistent).

## Non-Obvious Implementation

- Auth restore: `auth.client.ts` plugin calls `/users/me` on app start to restore session
- Proactive token refresh: `auth-refresh.client.ts` schedules a refresh before JWT expiry (`max(exp-60, 10)`s) and on `visibilitychange` (sleep/wake safety net); on failure, `refreshToken()` clears the session and `auth.client.ts` redirects to /
- Two-phase import: `analyzeFile()` → `ImportMappingForm` → `importWithMapping()`. Alias-mode groups force `SplitDuoAlias` type (4) and pass `aliasMappings` (alias name → alias GUID) in the mapping payload.
- **Alias mode**: `group.useAliases` (immutable after creation) switches members page to `AliasMembersList` (alias cards + finalize banner) and `ExpenseForm` to alias-based splits (`aliasSplits` payload). `useAliases` composable wraps the alias CRUD endpoints; `useBalances` enriches balances with alias metadata when `isAliasMode`.
- PWA: `@vite-pwa/nuxt` in `nuxt.config.ts` with manifest, workbox runtime caching, auto-update. `PwaUpdate.vue` prompts users on new deployments.

## Documentation Resources

MCP servers are available for up-to-date Nuxt and Nuxt UI docs:

- **Nuxt**: `mcp__nuxt-remote__*` tools (list pages, get docs, modules, etc.)
- **Nuxt UI**: `mcp__nuxt-ui-remote__*` tools (list/get components, composables, examples, etc.)

## Rules When Modifying This Code

- **Don't add Pinia** — use composable patterns with `ref()`/`readonly()`/`useState()`
- **Don't create custom UI components** when Nuxt UI provides one
- **Extract into components** when a template chunk has clear responsibility or would be repeated — don't leave everything inline in a single large template
- **Don't add scoped styles** — use Tailwind utility classes
- **Match existing patterns** — resource composables return `readonly()` state, catch errors internally, use `useNotifications`
- **Use `useApi()`** for all API calls — never call `$fetch` directly
- **Use `useModal()`** for confirmation dialogs — don't create ad-hoc modals
- **Mobile first** — design for small screens, enhance with responsive prefixes. Never desktop-first.
- **Run `pnpm lint:fix`** before considering any frontend task done
- **Error handling in event handlers** — resource composables already catch errors and show a toast. Event handlers that call composable methods should use `catch { // Error shown via toast }`, not `console.error` (redundant double-handling). See `NormalMembersList.vue` `confirmRoleChange` as the reference pattern.
- **Name fallbacks in modal copy** — when interpolating a user's name into confirmation modal text, use `user.firstName || user.fullName || ''` to avoid rendering `undefined` when `firstName` is empty. See `NormalMembersList.vue` as the reference pattern.
- **Self-action gating** — prefer `v-if` to hide action controls for the current user's own row/card rather than `:disabled`. Hiding is cleaner (no ghost button) and avoids over-disabling unrelated actions in the same dropdown. See `NormalMembersList.vue` as the reference pattern.
- **User settings** — live in `useState('user-settings')` via `useUserSettings` composable. `auth.client.ts` watches `user` and calls `syncFromUser` to apply the saved theme on any auth state change. Updates go via debounced PUT to `/users/me/settings`. Theme `"auto"` maps to Nuxt UI's `"system"` color-mode preference. `colorMode.storage` is set to `'cookie'` in `nuxt.config.ts` so the server-synced theme wins over stale localStorage on reload.
- **i18n — all user-facing strings via `$t()` / `useI18n()`** — never hardcode English in `.vue` files or composables. Use `$t('key')` in templates, `t('key')` from `useI18n()` in script. Locale files: `i18n/locales/en.json` + `it.json` (flat dotted keys, structurally identical). `@nuxtjs/i18n` v10 with `no_prefix` strategy, `lazy` loading. Nuxt UI v4 locale wired via `<UApp :locale="uiLocale">` in `app.vue` using `@nuxt/ui/locale`. Escape `@` in locale messages with `{'@'}` (ICU MessageFormat). No HTML in translation messages (use template-level tags instead).
- **i18n — language switching** — `useUserSettings` syncs `uiLanguage` ↔ `setLocale()` on auth state change and settings update. `LanguageSwitcher.vue` (in `components/settings/`) calls `setLocale()` + `settings.update()` + `refreshToken()` — the backend re-issues the JWT with the new `lang` claim. `base.ts` sends `Accept-Language` header on every API request matching the active locale. Backend returns a new token in the settings update response when `uiLanguage` changes.