# Implementation Plan: Frontend Test Infrastructure & Regression Suite

> Derived from `tasks/spec.md`. Follows `planning-and-task-breakdown` skill: dependency graph → vertical slicing → ordered tasks with checkpoints.

## Overview

Stand up Vitest infrastructure for `sd-frontend`, then add tests in priority order: pure utils (P0, immediate value) → auth/API spine composables (P0) → complex logic composables (P1) → resource CRUD (P2) → selective components (P2) → contract test (P3). No E2E. No browser. Runs on Fedora.

## Architecture Decisions

- **Vitest + happy-dom over Playwright/Cypress** — Fedora Playwright breakage is the hard constraint; happy-dom needs no browser binary. Vitest is the native Vite/Nuxt test runner. The API contract is already enforced by generated types (compile-time) + backend xUnit integration tests (runtime), so E2E would duplicate existing coverage.
- **Colocated `<file>.test.ts` over a separate `tests/` tree** — matches the TDD skill's "discover the stack" principle; keeps tests findable next to source; no separate import-path config.
- **`@nuxt/test-utils` vitest module for `mockNuxtImport`** — cleaner than manual `vi.mock` for auto-imported composables (`useState`, `useCookie`, `useApi`). Manual `vi.mock` only where `mockNuxtImport` doesn't suffice.
- **No coverage threshold in CI initially** — a green-but-untested codebase would fail CI on day one. Report on demand; gate later when the suite is mature.
- **Contract test as a separate `pnpm test:contract` script** — `gen:api` is slow; shouldn't block the dev watch loop. Runs in CI only.
- **Mock at boundaries, not internals** — mock `$fetch` (in `useApi` tests) and `useApi` (in resource tests), never the pure utils. Prefer real implementations per the TDD skill's preference order.

## Dependency Graph

```
vitest.config.ts + vitest.setup.ts + package.json scripts
    │
    ├── app/utils/*.test.ts          (no deps — pure functions)
    │
    ├── composables/api/base.test.ts  (deps: setup stubs for $fetch, useAuthToken)
    │       │
    │       └── composables/auth/useAuthToken.test.ts  (deps: setup stubs for useState, useCookie)
    │               │
    │               └── composables/auth/useAuth.test.ts  (deps: useApi + useAuthToken stubs)
    │
    ├── composables/utils/usePagination.test.ts   (no deps — pure state machine)
    ├── composables/utils/useErrorHandling.test.ts (deps: useNotifications stub)
    │
    ├── composables/resources/useImportExport.test.ts  (deps: useApi stub)
    │       │
    │       └── [other resource tests — replicate template]
    │
    ├── components/*.test.ts  (deps: @vue/test-utils + setup stubs — later phase)
    │
    └── contract test (gen:api staleness)  (no test deps — separate script)
```

Implementation order follows the graph bottom-up: infrastructure first, then pure utils (no deps), then the auth/API spine (foundation for resource tests), then complex composables, then resources, then components, then contract.

## Task List

### Phase 1: Foundation — Vitest infrastructure

- [ ] Task 1: Add Vitest dependencies and config
- [ ] Task 2: Write the global setup file (Nuxt runtime stubs)

### Checkpoint: Foundation
- [ ] `pnpm test` runs and exits 0 with a placeholder test
- [ ] `pnpm typecheck` and `pnpm lint:fix` still pass
- [ ] Review with human before proceeding

### Phase 2: Pure unit tests (P0 — immediate value)

- [ ] Task 3: `app/utils/currency.test.ts` — money math
- [ ] Task 4: `app/utils/date.test.ts` + `jwt.test.ts` + `userRoles.test.ts` + `withMinDuration.test.ts`
- [ ] Task 5: `app/utils/enumUtils.test.ts` — generic enum factory

### Checkpoint: Pure utils covered
- [ ] All `app/utils/*.test.ts` pass
- [ ] `pnpm test` green, < 5s
- [ ] Review with human before proceeding

### Phase 3: Auth & API spine (P0)

- [ ] Task 6: `composables/auth/useAuthToken.test.ts` — token bridge
- [ ] Task 7: `composables/api/base.test.ts` — useApi (401 refresh, dedup, headers)
- [ ] Task 8: `composables/auth/useAuth.test.ts` — login/logout/refresh/initialize

### Checkpoint: Auth/API spine covered
- [ ] `useApi`, `useAuth`, `useAuthToken` tests pass
- [ ] No regressions in existing utils tests
- [ ] Review with human before proceeding

### Phase 4: Complex composables (P1)

- [ ] Task 9: `composables/utils/usePagination.test.ts` — pagination state machine
- [ ] Task 10: `composables/utils/useErrorHandling.test.ts` — error extraction
- [ ] Task 11: `composables/resources/useImportExport.test.ts` — two-phase import

### Checkpoint: Complex composables covered
- [ ] P1 composable tests pass
- [ ] Full suite green
- [ ] Review with human before proceeding

### Phase 5: Resource CRUD (P2) + CI + docs

- [ ] Task 12: Resource composable tests (template-replicated: useGroups, useExpenses, useBalances, useUsers, useInvitations, useAliases)
- [ ] Task 13: Singleton + remaining composables (useCategories, usePaymentModes, use2FA, useReceiptScan, useUserSettings, useAiStatus)
- [ ] Task 14: Add `frontend-unit-tests` job to `.gitlab-ci.yml` verify stage
- [ ] Task 15: Document test rule in `sd-frontend/CLAUDE.md`

### Checkpoint: CI + docs
- [ ] CI runs `pnpm test` in verify stage
- [ ] CLAUDE.md documents the test rule
- [ ] Review with human before proceeding

### Phase 6: Selective component tests (P2) + contract (P3)

- [ ] Task 16: `ExpenseForm.test.ts` + `ImportMappingForm.test.ts` — complex form state
- [ ] Task 17: `ExpenseFilterCard.test.ts` + `DatePicker.test.ts` / `InputDate.test.ts`
- [ ] Task 18: Contract test — `gen:api` staleness check (`pnpm test:contract`)

### Checkpoint: Complete
- [ ] All acceptance criteria from spec met
- [ ] `pnpm test` < 10s, deterministic on repeated runs
- [ ] Ready for review

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `@nuxt/test-utils` `mockNuxtImport` doesn't cover a needed auto-import | Med | Fall back to manual `vi.mock` with a comment; keep stubs minimal in `vitest.setup.ts`. |
| `useApi` 401-refresh dedup logic (`_refreshPromise`) is hard to test in isolation | Med | Test the observable behavior (concurrent calls share one refresh), not the internal promise variable. State-based, not interaction-based, per TDD skill. |
| happy-dom diverges from real DOM on a component test | Low | Only affects the 4 selective component tests; if it blocks, skip that component test and note it — don't block the suite. |
| Resource composable tests become repetitive boilerplate | Low | Accept DAMP duplication per TDD skill; extract a tiny `mockUseApi()` helper only if 3+ tests repeat identical setup. |
| CI Fedora runners differ from dev Fedora env | Low | Vitest + happy-dom are Node-only, no system browser; CI parity is automatic. |
| `gen:api` contract test is slow and flakes on formatting diffs | Low | Separate `pnpm test:contract` script; run in CI only, not in the watch loop. |

## Parallelization Opportunities

- **Phase 2 (pure utils)** — Tasks 3, 4, 5 are independent; can be dispatched to parallel @fixer lanes once Phase 1 is complete.
- **Phase 4 (P1 composables)** — Tasks 9, 10, 11 are independent; parallelizable.
- **Phase 5 (resources)** — Task 12's resource composables are mutually independent; parallelizable across @fixer lanes once the template is established in Task 11.
- **Must be sequential:** Phase 1 (infra) before all; Phase 3 (auth spine) before Phase 4 resource tests that depend on `useApi` mocking patterns; Task 14 (CI) after the suite exists.

## Open Questions

(Carried from spec — to confirm with human before Phase 1)

1. Coverage gating now or deferred? **Recommendation: defer.**
2. `@nuxt/test-utils` vitest module vs. manual stubs? **Recommendation: include it.**
3. Contract test in `pnpm test` or separate script? **Recommendation: separate `pnpm test:contract`.**
4. Component test scope — 4 components right? **Recommendation: start with 4, expand only if regressions slip through.**