# Spec: Frontend Test Infrastructure & Regression Suite

## Objective

Add a test layer to `sd-frontend` that (1) catches regressions during development and (2) gives coding agents a concrete quality boundary. The suite must run fast, in CI, without a browser, on Fedora (where Playwright is currently broken).

**User stories:**
- As a developer, when I change `app/utils/currency.ts`, a test fails within seconds if I broke the split algorithm.
- As a developer, when I change `useApi` or `useAuth`, a test fails if the 401-refresh flow or token bridge regresses.
- As a coding agent, when I modify logic in `app/utils/` or `app/composables/`, `pnpm test` tells me whether my change is safe — I add/update tests for the logic I touched.
- As a maintainer, CI fails on MRs that break frontend logic — no manual browser check required for routine changes.

**Success looks like:**
- `pnpm test` runs in < 10s, exits 0 on green, non-zero on red.
- CI runs frontend unit tests in the `verify` stage alongside `lint` + `typecheck`.
- The highest-regression-risk code (currency math, auth, API layer, import flow, pagination) has passing tests.
- A documented rule tells agents what to test and when.

## Tech Stack

| Concern | Choice | Rationale |
|---|---|---|
| Test runner | **Vitest** | Native Vite integration with Nuxt 4; fastest TS test runner; built-in watch, coverage, mocking. |
| DOM environment | **happy-dom** | Lightweight, no browser binary; works on Fedora without Playwright. Faster than jsdom. |
| Component mounting | **@vue/test-utils** | Standard Vue 3 component testing; pairs with happy-dom. |
| Nuxt helpers | **@nuxt/test-utils** (vitest module only) | Provides `mockNuxtImport` for auto-imported composables and Nuxt runtime stubs. Used sparingly — most tests don't need it. |
| E2E | **None (Playwright excluded)** | Broken on Fedora; API contract already enforced by `openapi-typescript` (compile-time) + backend xUnit integration tests (runtime). SPA (`ssr: false`) means no SSR hydration bugs to catch. Revisit only if payment flows or cross-system user journeys appear. |

**Versions** (resolved at install time, pinned via `pnpm add -D`):
- `vitest` — latest stable (v2.x line)
- `happy-dom` — latest stable
- `@vue/test-utils` — v2.x
- `@nuxt/test-utils` — v3.x (vitest module + `mockNuxtImport`)
- `@vitest/coverage-v8` — optional, for coverage reporting

## Commands

```bash
# From sd-frontend/
pnpm test                 # run once (CI mode)
pnpm test:watch           # watch mode (dev)
pnpm test -- <path>       # single file: pnpm test app/utils/currency.test.ts
pnpm test -- --coverage   # with coverage report
pnpm lint:fix             # existing — run after test code changes
pnpm typecheck            # existing — run after test code changes
```

## Project Structure

```
sd-frontend/
  app/
    utils/
      currency.ts
      currency.test.ts          # NEW — colocated test
      date.test.ts              # NEW
      enumUtils.test.ts         # NEW
      jwt.test.ts               # NEW
      userRoles.test.ts         # NEW
      withMinDuration.test.ts   # NEW
    composables/
      api/
        base.test.ts            # NEW — useApi (mock $fetch)
      auth/
        useAuth.test.ts         # NEW
        useAuthToken.test.ts    # NEW
        use2FA.test.ts          # NEW (later phase)
      resources/
        useImportExport.test.ts # NEW — two-phase import state machine
        useGroups.test.ts       # NEW (later phase)
        useExpenses.test.ts     # NEW (later phase)
        ...                     # template-replicated for remaining resources
      utils/
        usePagination.test.ts   # NEW
        useErrorHandling.test.ts # NEW
    components/
      expenses/
        ExpenseForm.test.ts     # NEW (later phase — selective)
      ImportMappingForm.test.ts # NEW (later phase — selective)
  vitest.config.ts              # NEW — runner config
  vitest.setup.ts               # NEW — global stubs (useNuxtApp, useState, useCookie, useI18n)
  tasks/
    spec.md                     # this file
    plan.md                     # implementation plan
    todo.md                     # task checklist
```

**Colocation rule:** test files sit next to the source file, named `<file>.test.ts`. This matches the TDD skill's "discover the stack" principle and keeps tests findable without a separate `tests/` tree.

## Code Style

Test files follow the same TypeScript rules as source (`strict`, `verbatimModuleSyntax`, `import type`, no `any`). Example of the expected style:

```typescript
import { describe, it, expect } from 'vitest'
import { toMillis, fromMillis, splitMillis } from './currency'

describe('currency', () => {
  describe('toMillis / fromMillis', () => {
    it('round-trips integer millicents through decimal cents', () => {
      const millis = 12345
      expect(fromMillis(toMillis(millis))).toBe(millis)
    })
  })

  describe('splitMillis', () => {
    it('distributes remainder without losing or inventing millicents', () => {
      const total = 100
      const shares = splitMillis(total, 3)
      expect(shares.reduce((a, b) => a + b, 0)).toBe(total)
    })
  })
})
```

Conventions:
- `describe('<unitName>', () => { ... })` at top level; nested `describe` for logical groups.
- Test names read like specs: `it('rejects empty titles', ...)`, not `it('works', ...)`.
- Arrange-Act-Assert layout; one behavior per `it`.
- DAMP over DRY — each test self-contained; shared helpers only when setup is genuinely repetitive.
- Prefer real implementations over mocks; mock only at boundaries (`$fetch`, `useCookie`, Nuxt runtime).
- No snapshot tests.

## Testing Strategy

### Test Pyramid for this project

```
              ╱╲
             ╱  ╲        E2E: none (Playwright excluded)
            ╱────╲
           ╱      ╲      Component tests: ~10% (selective — only stateful components)
          ╱────────╲
         ╱          ╲    Composable tests: ~30% (logic cores, mocked API)
        ╱────────────╲
       ╱              ╲  Unit tests: ~60% (pure utils — the bulk)
      ╱────────────────╲
```

### What gets tested, by layer

**Layer 1 — Unit (pure utils) — highest ROI, ~60% of suite:**
- `app/utils/*` — all 6 files. Pure functions, no Nuxt runtime. Money math (`currency.ts`) is P0.
- These run in milliseconds, no mocking needed.

**Layer 2 — Composable logic (mocked API) — ~30% of suite:**
- `useApi` (`api/base.ts`) — mock `$fetch`; test 401→refresh→retry, `_refreshPromise` dedup, header injection, error propagation. P0.
- `useAuth` — mock `useApi` + `useAuthToken`; test login/logout/refresh state transitions, 2FA challenge token, initialize. P0.
- `useAuthToken` — mock `useState`/`useCookie`; test set/get/remove, bridge sync. P0.
- `useImportExport` — mock `useApi`; test two-phase state machine (analyze→map→import), CSV export blob, JSON field parsing. P1.
- `usePagination` — pure state machine; test next/prev/goToPage/setLimit boundaries. P1.
- `useErrorHandling` — mock `useNotifications`; test error extraction + classification. P1.
- Resource CRUD composables (`useGroups`, `useExpenses`, `useBalances`, `useUsers`, `useInvitations`, `useAliases`, `useCategories`, `usePaymentModes`, `useReceiptScan`, `useUserSettings`, `useAiStatus`) — P2, template-replicated: happy path + error path per CRUD op.
- `use2FA` — P2.

**Layer 3 — Component (selective) — ~10% of suite:**
- `ExpenseForm.vue` — multi-field, splits, alias-mode, receipt scan query params. Most complex form.
- `ImportMappingForm.vue` — two-phase import UI state.
- `ExpenseFilterCard.vue` — filter state emits.
- `DatePicker.vue` / `InputDate.vue` — date parsing edge cases.
- **Skip:** pure display components, ApexCharts wrappers, PWA lifecycle components, Nuxt UI primitives (tested upstream).

**Layer 4 — Contract/generation — cheap insurance:**
- `gen:api` staleness check — run `openapi-typescript`, diff against committed `app/types/api.d.ts`. Catches stale types after backend changes.
- Locale parity — already exists as `check:locales` script; wire into `pnpm test` or a separate `pnpm test:contract` script.

### What is NOT tested

- Nuxt UI v4 components (upstream-tested).
- ApexCharts rendering (visual, brittle).
- PWA install/offline lifecycle (browser-dependent, low ROI).
- Route middleware (`auth.ts`, `admin.ts`) — 5-line redirect guards; typecheck + manual smoke suffices.
- E2E / full user journeys — excluded by constraint (Playwright broken on Fedora) and justified by existing contract enforcement (generated types + backend integration tests).

### Mocking strategy

- `$fetch` — mock via `vi.fn()` in `useApi` tests; in resource composable tests, mock `useApi` itself.
- Nuxt runtime (`useState`, `useCookie`, `useNuxtApp`, `useI18n`, `navigateTo`, `useRoute`, `useRouter`) — stubbed in `vitest.setup.ts` via `@nuxt/test-utils` `mockNuxtImport` or manual `vi.mock`. Stubs are minimal and reset per test.
- `useNotifications` — stubbed to a no-op recorder in error-handling tests.
- No mocking of pure utils — call them directly.

### Coverage expectations

- No hard coverage threshold enforced initially (would fail CI on a green-but-untested codebase).
- Target: `app/utils/` at ~100% line coverage (pure functions, cheap).
- Target: `useApi`, `useAuth`, `useAuthToken`, `useImportExport`, `usePagination` at >80% line coverage.
- Coverage reported on demand (`pnpm test -- --coverage`), not gated in CI until the suite matures.

## Boundaries

**Always do:**
- Run `pnpm test` before considering a frontend task done (alongside existing `pnpm lint:fix` + `pnpm typecheck`).
- Add or update tests when changing logic in `app/utils/` or `app/composables/`.
- Colocate tests next to source (`<file>.test.ts`).
- Follow Arrange-Act-Assert; one behavior per `it`.
- Name tests descriptively (read like specs).
- Prefer real implementations over mocks; mock only at boundaries.

**Ask first:**
- Adding a new test dependency (beyond the four listed in Tech Stack).
- Changing `vitest.config.ts` in a way that affects all tests.
- Introducing E2E (Playwright/Cypress) — currently excluded by constraint.
- Enforcing a coverage threshold in CI.
- Testing Nuxt UI v4 internals (don't — test upstream).

**Never do:**
- Skip or disable tests to make the suite pass.
- Commit with `pnpm test` red.
- Mock everything (tests pass but production breaks).
- Use snapshot tests.
- Test framework code instead of application code.
- Edit `app/types/api.d.ts` by hand (regenerate via `pnpm gen:api`).
- Call `$fetch` directly in app code (always via `useApi()`) — tests enforce this by mocking `useApi`, not `$fetch`, in resource tests.

## Success Criteria

- [ ] `pnpm test` runs in < 10s and exits 0 on a clean tree.
- [ ] `vitest.config.ts` + `vitest.setup.ts` committed; `pnpm test` + `pnpm test:watch` scripts in `package.json`.
- [ ] All 6 `app/utils/*.test.ts` files pass.
- [ ] `useApi`, `useAuth`, `useAuthToken` tests pass (P0 composable coverage).
- [ ] `useImportExport`, `usePagination`, `useErrorHandling` tests pass (P1).
- [ ] CI `.gitlab-ci.yml` runs `pnpm test` in the `verify` stage for frontend.
- [ ] `sd-frontend/CLAUDE.md` documents the test rule (when to test, what to test, commands).
- [ ] No Playwright, no browser binary dependency.
- [ ] Suite is deterministic — no flaky tests on repeated runs.

## Open Questions

1. **Coverage gating** — enforce a threshold in CI now, or defer until the suite is mature? Recommendation: defer. Add a `pnpm test -- --coverage` script for on-demand reporting only.
2. **`@nuxt/test-utils` vitest module vs. manual stubs** — `@nuxt/test-utils` provides `mockNuxtImport` which is cleaner for auto-imported composables, but adds a dependency and config. Recommendation: include it; fall back to manual `vi.mock` only where `mockNuxtImport` doesn't suffice.
3. **Contract test for `gen:api`** — wire into `pnpm test` (runs every time) or a separate `pnpm test:contract` script (runs in CI only)? Recommendation: separate script; `gen:api` is slow and shouldn't block the dev watch loop.
4. **Component test scope** — confirm the 4 components listed (ExpenseForm, ImportMappingForm, ExpenseFilterCard, DatePicker/InputDate) are the right selective set, or expand/contract? Recommendation: start with these 4; add more only if regressions slip through.