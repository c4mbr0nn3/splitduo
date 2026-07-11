# CLAUDE.md — sd-frontend

## Overview

Nuxt 4 SPA (SSR disabled) with Vue 3 Composition API. Uses Nuxt UI v4 as the component/design system. No TypeScript in application code — all composables, components, and utils are plain JavaScript with runtime prop validation.

## Commands

```bash
pnpm dev            # Dev server on http://localhost:3000
pnpm generate       # Static build → .output/public/ (deployed into backend's wwwroot)
pnpm lint           # ESLint check
pnpm lint:fix       # ESLint autofix
pnpm install        # Install dependencies
```

> **After every frontend implementation**, run `pnpm lint:fix` to auto-fix style issues before considering the task done.

## Tech Stack

- **Nuxt 4** (`^4.4.8`) — SPA mode (`ssr: false`)
- **Nuxt UI v4** (`^4.9.0`) — TailwindCSS v4, component library, color mode, overlays
- **Icons** — Lucide (`@iconify-json/lucide`) + Simple Icons (`@iconify-json/simple-icons`)
- **ESLint** — `@nuxt/eslint` with stylistic rules enabled
- **ApexCharts** (`apexcharts` + `vue3-apexcharts`) — charting for group statistics
- **PWA** (`@vite-pwa/nuxt`) — installable app, offline support, auto-update
- **uqr** — QR code generation for 2FA

No Pinia. No custom CSS framework. No TypeScript in `.vue`/`.js` files.

## Project Structure

```
app/
├── app.vue                    # Root component
├── app.config.ts              # Nuxt UI theme (primary: violet, neutral: zinc)
├── assets/css/main.css        # Tailwind imports, custom font & color tokens
├── components/
│   ├── ChangePasswordModal.vue
│   ├── ImportAnalysisResults.vue
│   ├── ImportMappingForm.vue
│   ├── PwaUpdate.vue
│   ├── admin/                 # UserCard, UserCardSkeleton, UserForm
│   ├── button/                # ColorMode toggle
│   ├── dashboard/             # GroupCard, GroupCardSkeleton, StatCard, StatCardSkeleton
│   ├── expenses/              # ExpenseForm
│   ├── groups/                # ActionsDropdown, ExpenseCard, ExpenseCardSkeleton, ExpenseFilterCard,
│   │                          # ExpensesTab, GroupCardSkeleton, GroupForm, InviteUsersForm,
│   │                          # MemberBalanceCard, SectionHeader, StatsCards, StatsCategoryChart,
│   │                          # StatsMemberPaidChart, StatsMonthlyChart, StatsTab, TabsNav, UserBalanceCard
│   │   └── members/           # Card, List
│   ├── layout/                # AppHeader, LogoutButton
│   └── ui/                    # ButtonDropdown, CardHeader, DatePicker, EmptyState, GenericModal,
│                              # InputDate, LoadingSpinner, ReceiptPreviewModal, ScanReceiptButton
├── composables/
│   ├── api/base.js            # Base API client (useApi)
│   ├── auth/                  # use2FA, useAuth, useAuthToken
│   ├── resources/             # useAiStatus, useBalances, useCategories, useExpenses, useGroups,
│   │                          # useImportExport, useInvitations, usePaymentModes, useReceiptScan, useUsers
│   ├── ui/                    # useChartTheme, useModal
│   ├── utils/                 # useNotifications, useErrorHandling, usePagination, useDebounceSearch
│   └── index.js               # Barrel export for all composables
├── layouts/
│   ├── auth.vue               # Minimal centered layout (login, reset password)
│   └── default.vue            # AppHeader + content
├── middleware/
│   ├── auth.js                # Redirect to / if not authenticated
│   └── admin.js               # Redirect to /dashboard if not admin (globalRoleId !== 2)
├── pages/                     # File-based routing (see Routes below)
├── plugins/
│   ├── apexcharts.client.js   # Client-only: registers ApexCharts Vue component globally
│   └── auth.client.js         # Client-only: initializes auth state on app start
└── utils/
    ├── currency.js            # Currency formatting
    ├── date.js                # Date formatting
    ├── enumUtils.js           # Generic enum factory
    ├── userRoles.js            # User role enums
    └── withMinDuration.js      # Async duration helper
```

## Routes

| Path | Page | Layout | Middleware | Purpose |
|---|---|---|---|---|
| `/` | `index.vue` | auth | — | Login |
| `/forgot-password` | | auth | — | Request password reset |
| `/reset-password` | | auth | — | Reset password form |
| `/auth/verify` | | auth | — | 2FA verification |
| `/invite/accept` | | auth | — | Accept group invitation |
| `/dashboard` | | default | auth | Main dashboard |
| `/profile` | | default | auth | User profile |
| `/settings/2fa/setup` | | default | auth | 2FA setup |
| `/groups` | | default | auth | List groups |
| `/groups/add` | | default | auth | Create group |
| `/groups/[id]` | | default | auth | Group detail + expenses |
| `/groups/[id]/edit` | | default | auth | Edit group |
| `/groups/[id]/invite` | | default | auth | Invite users |
| `/groups/[id]/members` | | default | auth | Manage members |
| `/groups/[id]/imports` | | default | auth | Import history |
| `/groups/[id]/imports/add` | | default | auth | Two-phase CSV import |
| `/groups/[id]/expenses/[expenseId]/edit` | | default | auth | Edit expense |
| `/expenses/add` | | default | auth | Add expense (?groupId=) |
| `/admin/users` | | default | auth, admin | User management |
| `/admin/users/[id]/edit` | | default | auth, admin | Edit user |

## API Layer

**Base client** (`composables/api/base.js`):
- Wraps Nuxt's `$fetch` with auth headers
- Dev base URL: `http://localhost:8080/api/v1` — Prod: `/api/v1`
- Auto-attaches `Bearer` token from cookie
- Methods: `get(endpoint, params)`, `post(endpoint, body)`, `put(endpoint, body)`, `delete(endpoint)`
- Errors thrown as `createError({ statusCode, statusMessage })`

**Backend response shape** (expected by all resource composables):
```json
{ "success": true, "data": {}, "error": null, "pagination": {} }
```

## Composable Patterns

### Resource composables (`composables/resources/`)

Each wraps API calls for a domain entity. Pattern:

```javascript
export default function useGroups() {
  const groups = ref([])
  const isLoading = ref(false)
  const api = useApi()

  const fetchGroups = async () => {
    isLoading.value = true
    try {
      const response = await api.get('/groups')
      groups.value = response.data
    } catch (error) { /* toast error */ }
    finally { isLoading.value = false }
  }

  return {
    groups: readonly(groups),
    isLoading: readonly(isLoading),
    fetchGroups,
    // ... other CRUD methods
  }
}
```

Key rules:
- Internal state via `ref()`, exposed via `readonly()`
- `isLoading` pattern on every async operation
- Errors caught internally, shown via `useNotifications().showError()`
- `useExpenses(groupId)` accepts reactive params via `toRef()`

Resource composables: `useGroups`, `useExpenses`, `useCategories`, `usePaymentModes`, `useBalances`, `useUsers`, `useImportExport`, `useInvitations` (invitation management), `useAiStatus` (AI service availability check), `useReceiptScan` (receipt scanning via AI).

### Singleton composables (`useCategories`, `usePaymentModes`)

Global `ref()` declared **outside** the composable function — shared across all callers. Auto-fetches on first use, caches result.

### Auth composables

- **`useAuthToken`** — Token CRUD in cookies (`auth-token`, `refresh-token`)
- **`useAuth`** — Login/logout/refresh/initialize. User stored in cookie + `useState('user')`. Exposes `isAuthenticated`, `isGlobalAdmin` computed.
- **`use2FA`** — 2FA login flow, backup codes, TOTP setup/verify

### Utility composables

- **`useNotifications`** — `showSuccess()`, `showError()`, `showWarning()`, `showInfo()` (wraps Nuxt UI `useToast`)
- **`useErrorHandling`** — `handleApiError()`, `handleValidationErrors()`, `handleAuthError()`
- **`usePagination`** — Factory: `createPaginatedList(data)` → items, pagination state, nav methods
- **`useDebounceSearch`** — Debounced search query ref (uses `@vueuse/core`)
- **`useModal`** — Programmatic modals via Nuxt UI `useOverlay()`. Returns `Promise<boolean>`
- **`useChartTheme`** — ApexCharts theme integration with Nuxt UI color mode

## Component Conventions

- All components use `<script setup>` (Composition API only)
- Props validated at runtime with `defineProps({ name: { type: String, required: true } })`
- Auto-imported by Nuxt — folder nesting creates name prefixes (e.g., `groups/ExpenseCard.vue` → `<GroupsExpenseCard />`)
- Forms emit `@submit` and `@cancel` events
- Use Nuxt UI components (`UButton`, `UInput`, `UCard`, `UModal`, etc.) — don't build custom equivalents
- **Prefer components over inline markup** — when a piece of template has a clear responsibility or would otherwise be repeated, extract it into a focused component. This keeps templates readable and maintainable. Place reusable primitives in `components/ui/`, domain-specific pieces in their feature folder (e.g., `components/groups/`).

## Styling

- **TailwindCSS v4** via Nuxt UI — utility-first classes in templates
- **Theme**: primary=violet, neutral=zinc (set in `app.config.ts`)
- **Custom font**: Public Sans (set in `main.css` via `@theme`)
- **Dark mode**: Built-in via Nuxt UI color mode, toggled with `<UColorModeButton />`
- **No scoped styles, no CSS modules** — pure utility classes
- **Mobile first**: Design for small screens first — use Tailwind's responsive prefixes (`sm:`, `md:`, `lg:`) to progressively enhance for larger viewports. Never design desktop-first and patch for mobile.

## State Management

No Pinia. State is managed through:

1. **Composable local state** — `ref()` inside composable, returned as `readonly()`
2. **Singleton refs** — Global `ref()` outside composable (categories, payment modes)
3. **`useState()`** — Nuxt's SSR-safe global state (used for auth user)
4. **`useCookie()`** — Persistent state across refreshes (auth tokens, user data)

## Key Implementation Details

**Auth flow**: Login → store tokens in cookies → `useState('user')` for reactivity → `auth.client.js` plugin restores state on refresh via `/users/me` endpoint.

**Two-phase import**: Upload CSV → `analyzeFile()` returns unmatched entities → user provides mappings via `ImportMappingForm` → `importWithMapping()` creates expenses.

**Expense splitting**: `ExpenseForm` handles real-time proportional split calculations, "Distribute Remaining" and "Split Equally" actions, per-user validation.

**File exports**: Create `Blob` from API response, trigger browser download. Supports CSV and Cospend JSON formats.

**2FA**: TOTP-based, with backup codes and email verification codes. Setup at `/settings/2fa/setup`, verification at `/auth/verify`.

**Invitations**: Email-based group invitations with time-limited secure links. Accept at `/invite/accept`.

**PWA**: `@vite-pwa/nuxt` module configured in `nuxt.config.ts` with manifest, workbox runtime caching, and auto-update behavior. `PwaUpdate.vue` component prompts users on new deployments.

## Documentation Resources

MCP servers are available for up-to-date Nuxt and Nuxt UI docs:

- **Nuxt**: `mcp__nuxt-remote__*` tools (list pages, get docs, modules, etc.)
- **Nuxt UI**: `mcp__nuxt-ui-remote__*` tools (list/get components, composables, examples, etc.)

## Rules When Modifying This Code

- **Don't add TypeScript** to `.vue` or `.js` files — project uses plain JS with runtime validation
- **Don't add Pinia** — use composable patterns with `ref()`/`readonly()`/`useState()`
- **Don't create custom UI components** when Nuxt UI provides one
- **Extract into components** when a template chunk has a clear responsibility or would otherwise be repeated — don't leave everything inline in a single large template
- **Don't add scoped styles** — use Tailwind utility classes
- **Match existing patterns** — resource composables return `readonly()` state, catch errors internally, use `useNotifications`
- **Use `useApi()`** for all API calls — never call `$fetch` directly
- **Use `useModal()`** for confirmation dialogs — don't create ad-hoc modals
