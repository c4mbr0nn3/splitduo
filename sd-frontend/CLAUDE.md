# CLAUDE.md — sd-frontend

## Overview

Nuxt 4 SPA (ssr: false), Vue 3 Composition API, Nuxt UI v4, TailwindCSS v4. **Plain JS — no TypeScript** in `.vue`/`.js` files. No Pinia.

## Commands

- `pnpm dev` — http://localhost:3000
- `pnpm generate` — static build → `.output/public/` (deployed into backend wwwroot)
- `pnpm lint:fix` — **run after every implementation** before considering task done

## Tech Stack

- Nuxt 4 (`^4.4.8`), Nuxt UI v4 (`^4.9.0`), TailwindCSS v4
- Icons: Lucide + Simple Icons (`@iconify-json/*`)
- ApexCharts (`apexcharts` + `vue3-apexcharts`) — group statistics charting
- PWA (`@vite-pwa/nuxt`) — installable app, offline support, auto-update
- uqr — QR code generation for 2FA
- ESLint (`@nuxt/eslint` with stylistic rules)

## Structure

- `app/components/` — auto-imported, folder nesting = name prefix (`groups/ExpenseCard.vue` → `<GroupsExpenseCard />`). Subfolders: `admin/`, `button/`, `dashboard/`, `expenses/`, `groups/` (+ `members/`), `layout/`, `ui/` (primitives)
- `app/composables/` — `api/base.js` (useApi, incl. `getBlob` for binary downloads), `auth/` (useAuth, useAuthToken, use2FA), `resources/` (one per entity, incl. `useAliases`), `ui/` (useModal, useChartTheme), `utils/` (useNotifications, useErrorHandling, usePagination, useDebounceSearch), `index.js` (barrel)
- `app/pages/` — file-based routing, nested folders = nested routes
- `app/middleware/` — `auth.js` (redirect to / if not authenticated), `admin.js` (redirect to /dashboard if not admin)
- `app/plugins/` — `auth.client.js` (restores auth state on app start), `auth-refresh.client.js` (proactive token refresh: timer + visibilitychange), `apexcharts.client.js` (registers ApexCharts globally)
- `app/utils/` — currency, date, enumUtils, jwt, userRoles, withMinDuration

## API Layer

`useApi()` (`composables/api/base.js`) wraps `$fetch` with auth headers. Dev: `http://localhost:8080/api/v1`, prod: `/api/v1`. Methods: `get/post/put/delete`. **Never call `$fetch` directly** — always use `useApi()`.

Backend response shape: `{ success, data, error, pagination }` — see backend CLAUDE.md for details.

## Composable Patterns

**Resource composables** (`resources/`): one per entity. Internal state via `ref()`, exposed via `readonly()`. `isLoading` on every async op. Errors caught internally → `useNotifications().showError()`. `useExpenses(groupId)` accepts reactive params via `toRef()`.

**Singleton composables** (`useCategories`, `usePaymentModes`): global `ref()` declared outside function — shared across callers, auto-fetches on first use.

**Auth**: `useAuthToken` (token in `useState` bridged to `useCookie` for persistence — synchronous shared source of truth; the 7d cookie `maxAge` is intentional because `/auth/refresh` needs the expired JWT), `useAuth` (login/logout/refresh, user in cookie + `useState('user')`, exposes `isAuthenticated`/`isGlobalAdmin`), `use2FA` (TOTP/backup/email flow). Proactive refresh via `plugins/auth-refresh.client.js` (timer + `visibilitychange`); `utils/jwt.js` decodes `exp` for scheduling.

**Utility**: `useNotifications` (toast wrapper), `useErrorHandling`, `usePagination` (factory), `useDebounceSearch` (@vueuse/core), `useModal` (Nuxt UI useOverlay, returns Promise<boolean>), `useChartTheme`, `useSmartBack(parentRoute)` (smart back nav — `router.back()` if in-app history exists, else `navigateTo(parentRoute, { replace: true })`; powers `UiCardHeader`'s `backTo` prop and page-level cancel handlers).

## Component Conventions

- `<script setup>` only. Props via runtime `defineProps({ ... })` (no TS).
- Auto-imported: folder nesting = name prefix (`groups/ExpenseCard.vue` → `<GroupsExpenseCard />`)
- Use Nuxt UI components (`UButton`, `UInput`, `UCard`, `UModal`) — don't build custom equivalents
- Forms emit `@submit` and `@cancel`
- Extract into components when template chunk has clear responsibility or repeats. Primitives → `components/ui/`, domain → feature folder

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

- Auth restore: `auth.client.js` plugin calls `/users/me` on app start to restore session
- Proactive token refresh: `auth-refresh.client.js` schedules a refresh before JWT expiry (`max(exp-60, 10)`s) and on `visibilitychange` (sleep/wake safety net); on failure, `refreshToken()` clears the session and `auth.client.js` redirects to /
- Two-phase import: `analyzeFile()` → `ImportMappingForm` → `importWithMapping()`. Alias-mode groups force `SplitDuoAlias` type (4) and pass `aliasMappings` (alias name → alias GUID) in the mapping payload.
- **Alias mode**: `group.useAliases` (immutable after creation) switches members page to `AliasMembersList` (alias cards + finalize banner) and `ExpenseForm` to alias-based splits (`aliasSplits` payload). `useAliases` composable wraps the alias CRUD endpoints; `useBalances` enriches balances with alias metadata when `isAliasMode`.
- PWA: `@vite-pwa/nuxt` in `nuxt.config.ts` with manifest, workbox runtime caching, auto-update. `PwaUpdate.vue` prompts users on new deployments.

## Documentation Resources

MCP servers are available for up-to-date Nuxt and Nuxt UI docs:

- **Nuxt**: `mcp__nuxt-remote__*` tools (list pages, get docs, modules, etc.)
- **Nuxt UI**: `mcp__nuxt-ui-remote__*` tools (list/get components, composables, examples, etc.)

## Rules When Modifying This Code

- **Don't add TypeScript** to `.vue` or `.js` files — project uses plain JS with runtime validation
- **Don't add Pinia** — use composable patterns with `ref()`/`readonly()`/`useState()`
- **Don't create custom UI components** when Nuxt UI provides one
- **Extract into components** when a template chunk has clear responsibility or would be repeated — don't leave everything inline in a single large template
- **Don't add scoped styles** — use Tailwind utility classes
- **Match existing patterns** — resource composables return `readonly()` state, catch errors internally, use `useNotifications`
- **Use `useApi()`** for all API calls — never call `$fetch` directly
- **Use `useModal()`** for confirmation dialogs — don't create ad-hoc modals
- **Mobile first** — design for small screens, enhance with responsive prefixes. Never desktop-first.
- **Run `pnpm lint:fix`** before considering any frontend task done