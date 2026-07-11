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
- `app/composables/` — `api/base.js` (useApi), `auth/` (useAuth, useAuthToken, use2FA), `resources/` (one per entity), `ui/` (useModal, useChartTheme), `utils/` (useNotifications, useErrorHandling, usePagination, useDebounceSearch), `index.js` (barrel)
- `app/pages/` — file-based routing, nested folders = nested routes
- `app/middleware/` — `auth.js` (redirect to / if not authenticated), `admin.js` (redirect to /dashboard if not admin)
- `app/plugins/` — `auth.client.js` (restores auth state on app start), `apexcharts.client.js` (registers ApexCharts globally)
- `app/utils/` — currency, date, enumUtils, userRoles, withMinDuration

## API Layer

`useApi()` (`composables/api/base.js`) wraps `$fetch` with auth headers. Dev: `http://localhost:8080/api/v1`, prod: `/api/v1`. Methods: `get/post/put/delete`. **Never call `$fetch` directly** — always use `useApi()`.

Backend response shape: `{ success, data, error, pagination }` — see backend CLAUDE.md for details.

## Composable Patterns

**Resource composables** (`resources/`): one per entity. Internal state via `ref()`, exposed via `readonly()`. `isLoading` on every async op. Errors caught internally → `useNotifications().showError()`. `useExpenses(groupId)` accepts reactive params via `toRef()`.

**Singleton composables** (`useCategories`, `usePaymentModes`): global `ref()` declared outside function — shared across callers, auto-fetches on first use.

**Auth**: `useAuthToken` (cookie token CRUD), `useAuth` (login/logout/refresh, user in cookie + `useState('user')`, exposes `isAuthenticated`/`isGlobalAdmin`), `use2FA` (TOTP/backup/email flow).

**Utility**: `useNotifications` (toast wrapper), `useErrorHandling`, `usePagination` (factory), `useDebounceSearch` (@vueuse/core), `useModal` (Nuxt UI useOverlay, returns Promise<boolean>), `useChartTheme`.

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

## State

No Pinia. State via: composable-local `ref()` → `readonly()`, singleton refs (outside function), `useState()` (auth user), `useCookie()` (tokens, persistent).

## Non-Obvious Implementation

- Auth restore: `auth.client.js` plugin calls `/users/me` on app start to restore session
- Two-phase import: `analyzeFile()` → `ImportMappingForm` → `importWithMapping()`
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