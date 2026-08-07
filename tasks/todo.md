# Todo: Frontend Test Infrastructure & Regression Suite

> Checklist companion to `tasks/plan.md`. Each task has acceptance criteria, verification, dependencies, and files. Follows `planning-and-task-breakdown` skill task template.

## Phase 1: Foundation — Vitest infrastructure

### Task 1: Add Vitest dependencies and config
- [ ] **Acceptance:**
  - [ ] `vitest`, `happy-dom`, `@vue/test-utils`, `@nuxt/test-utils` added to `sd-frontend/package.json` `devDependencies`
  - [ ] `vitest.config.ts` created at `sd-frontend/vitest.config.ts` with `environment: 'happy-dom'`, alias `@/` → `app/`, `setupFiles: ['./vitest.setup.ts']`
  - [ ] `pnpm test` script (`vitest run`) and `pnpm test:watch` script (`vitest`) added to `package.json`
  - [ ] `pnpm test:contract` script placeholder added (filled in Task 18)
  - [ ] A placeholder test (`describe('placeholder', () => { it('runs', () => expect(1).toBe(1)) })`) passes
- [ ] **Verify:** `pnpm test` exits 0; `pnpm typecheck` still passes; `pnpm lint:fix` clean
- [ ] **Dependencies:** None
- [ ] **Files:** `sd-frontend/package.json`, `sd-frontend/vitest.config.ts`, `sd-frontend/vitest.setup.ts` (empty placeholder)
- [ ] **Scope:** S (1-2 files + config)

### Task 2: Write the global setup file (Nuxt runtime stubs)
- [ ] **Acceptance:**
  - [ ] `vitest.setup.ts` stubs: `useState`, `useCookie`, `useNuxtApp`, `useI18n` (`t` returns key), `navigateTo`, `useRoute`, `useRouter`, `defineNuxtPlugin`
  - [ ] Stubs reset between tests (via `beforeEach` in setup or `vi.resetAllMocks()`)
  - [ ] A composable test using `useState`/`useCookie` (e.g. a trivial `useAuthToken` smoke test) passes against the stubs
- [ ] **Verify:** `pnpm test` green; stubs don't leak state between tests (run suite twice, same result)
- [ ] **Dependencies:** Task 1
- [ ] **Files:** `sd-frontend/vitest.setup.ts`
- [ ] **Scope:** S (1 file)

### Checkpoint: Foundation
- [ ] `pnpm test` runs and exits 0 with the placeholder + smoke test
- [ ] `pnpm typecheck` and `pnpm lint:fix` still pass
- [ ] **Review with human before proceeding**

## Phase 2: Pure unit tests (P0 — immediate value)

### Task 3: `app/utils/currency.test.ts` — money math
- [ ] **Acceptance:**
  - [ ] `toMillis` / `fromMillis` round-trip tested
  - [ ] `splitMillis` tested: sum of shares equals input; remainder distributed without loss/invention; edge cases (1 share, 0 total, large totals)
  - [ ] `rescaleMillis` tested
  - [ ] `formatAmount` / `formatCurrency` tested with locale stubs (mock `useNuxtApp` locale)
- [ ] **Verify:** `pnpm test app/utils/currency.test.ts` green; full suite green
- [ ] **Dependencies:** Task 2 (for `formatAmount` locale stub)
- [ ] **Files:** `sd-frontend/app/utils/currency.test.ts`
- [ ] **Scope:** S (1 file)

### Task 4: `date.test.ts` + `jwt.test.ts` + `userRoles.test.ts` + `withMinDuration.test.ts`
- [ ] **Acceptance:**
  - [ ] `date.ts`: `formatDate`, `formatDateString`, `formatDuration` — timezone/locale edge cases
  - [ ] `jwt.ts`: `decodeJwtExp` — valid token, malformed token, missing `exp` claim
  - [ ] `userRoles.ts`: `UserRole` enum values, `UserRoleLabels`, `getUserRoleOptions`
  - [ ] `withMinDuration.ts`: resolves after min duration; resolves immediately if work takes longer
- [ ] **Verify:** `pnpm test` green for all 4 new files
- [ ] **Dependencies:** Task 2 (date.ts uses `useNuxtApp` locale)
- [ ] **Files:** `sd-frontend/app/utils/date.test.ts`, `sd-frontend/app/utils/jwt.test.ts`, `sd-frontend/app/utils/userRoles.test.ts`, `sd-frontend/app/utils/withMinDuration.test.ts`
- [ ] **Scope:** S (4 small files)

### Task 5: `app/utils/enumUtils.test.ts` — generic enum factory
- [ ] **Acceptance:**
  - [ ] `createEnum` — labels, select options, validation (valid/invalid values)
  - [ ] `createSimpleEnum` — same coverage
  - [ ] `createEnumFromValues` — same coverage
  - [ ] Edge cases: empty values, duplicate labels
- [ ] **Verify:** `pnpm test app/utils/enumUtils.test.ts` green
- [ ] **Dependencies:** Task 1
- [ ] **Files:** `sd-frontend/app/utils/enumUtils.test.ts`
- [ ] **Scope:** S (1 file)

### Checkpoint: Pure utils covered
- [ ] All 6 `app/utils/*.test.ts` pass
- [ ] `pnpm test` green, < 5s
- [ ] **Review with human before proceeding**

## Phase 3: Auth & API spine (P0)

### Task 6: `composables/auth/useAuthToken.test.ts` — token bridge
- [ ] **Acceptance:**
  - [ ] `setToken` updates both `useState` and `useCookie`
  - [ ] `getToken` reads from `useState` (sync source)
  - [ ] `removeToken` clears both
  - [ ] Bridge sync: cookie change reflects in state and vice versa
- [ ] **Verify:** `pnpm test composables/auth/useAuthToken.test.ts` green
- [ ] **Dependencies:** Task 2 (useState/useCookie stubs)
- [ ] **Files:** `sd-frontend/app/composables/auth/useAuthToken.test.ts`
- [ ] **Scope:** S (1 file)

### Task 7: `composables/api/base.test.ts` — useApi (401 refresh, dedup, headers)
- [ ] **Acceptance:**
  - [ ] `get<T>` / `getPaginated<T>` / `post<T>` / `put<T>` / `delete` / `getBlob` call `$fetch` with correct URL + method
  - [ ] Auth header (`Authorization: Bearer <token>`) injected from `useAuthToken`
  - [ ] `Accept-Language` header injected from active locale
  - [ ] 401 response triggers refresh → retry once; success after retry returns data
  - [ ] 401 after refresh failure propagates error (clears session)
  - [ ] Concurrent 401s share a single `_refreshPromise` (dedup) — test observable behavior, not the internal variable
  - [ ] Non-401 errors propagate unchanged
- [ ] **Verify:** `pnpm test composables/api/base.test.ts` green
- [ ] **Dependencies:** Task 2, Task 6 (useAuthToken stub)
- [ ] **Files:** `sd-frontend/app/composables/api/base.test.ts`
- [ ] **Scope:** M (1 file, complex — multiple behaviors)

### Task 8: `composables/auth/useAuth.test.ts` — login/logout/refresh/initialize
- [ ] **Acceptance:**
  - [ ] `login` with valid credentials sets user + token, returns success
  - [ ] `login` with 2FA challenge sets challenge token, does not set user
  - [ ] `logout` clears user + token
  - [ ] `refreshToken` updates token, preserves user
  - [ ] `refreshToken` failure clears session
  - [ ] `initialize` restores session from stored token (calls `/users/me`)
  - [ ] `isAuthenticated` / `isGlobalAdmin` computed from user state
- [ ] **Verify:** `pnpm test composables/auth/useAuth.test.ts` green
- [ ] **Dependencies:** Task 7 (useApi mock pattern established)
- [ ] **Files:** `sd-frontend/app/composables/auth/useAuth.test.ts`
- [ ] **Scope:** M (1 file, multiple state transitions)

### Checkpoint: Auth/API spine covered
- [ ] `useApi`, `useAuth`, `useAuthToken` tests pass
- [ ] No regressions in utils tests
- [ ] **Review with human before proceeding**

## Phase 4: Complex composables (P1)

### Task 9: `composables/utils/usePagination.test.ts` — pagination state machine
- [ ] **Acceptance:**
  - [ ] `createPaginatedList<T>` initial state
  - [ ] `next` / `prev` / `goToPage` / `setLimit` boundary cases (first page, last page, out-of-range)
  - [ ] State updates are consistent (page/limit/totalPages)
- [ ] **Verify:** `pnpm test composables/utils/usePagination.test.ts` green
- [ ] **Dependencies:** Task 1
- [ ] **Files:** `sd-frontend/app/composables/utils/usePagination.test.ts`
- [ ] **Scope:** S (1 file)

### Task 10: `composables/utils/useErrorHandling.test.ts` — error extraction
- [ ] **Acceptance:**
  - [ ] `handleApiError` extracts message from FetchError-like object (`statusCode`, `data.message`)
  - [ ] `handleValidationErrors` extracts field-level validation errors
  - [ ] `handleAuthError` classifies 401 vs other auth errors
  - [ ] All call `useNotifications().showError()` with extracted message (stub recorder)
- [ ] **Verify:** `pnpm test composables/utils/useErrorHandling.test.ts` green
- [ ] **Dependencies:** Task 2 (useNotifications stub)
- [ ] **Files:** `sd-frontend/app/composables/utils/useErrorHandling.test.ts`
- [ ] **Scope:** S (1 file)

### Task 11: `composables/resources/useImportExport.test.ts` — two-phase import
- [ ] **Acceptance:**
  - [ ] `analyzeFile` — uploads file, stores analysis result, advances phase
  - [ ] `importWithMapping` — sends mapping payload, clears analysis on success
  - [ ] State machine: idle → analyzing → analyzed → importing → done (and error transitions)
  - [ ] CSV export: `getBlob` called, blob returned
  - [ ] JSON field parsing: `ImportAnalysis` / `KeyValue` parsed from JSON string
  - [ ] Alias-mode: `SplitDuoAlias` type (4) + `aliasMappings` in payload
- [ ] **Verify:** `pnpm test composables/resources/useImportExport.test.ts` green
- [ ] **Dependencies:** Task 7 (useApi mock pattern)
- [ ] **Files:** `sd-frontend/app/composables/resources/useImportExport.test.ts`
- [ ] **Scope:** M (1 file, complex state machine)

### Checkpoint: Complex composables covered
- [ ] P1 composable tests pass
- [ ] Full suite green
- [ ] **Review with human before proceeding**

## Phase 5: Resource CRUD (P2) + CI + docs

### Task 12: Resource composable tests (template-replicated)
- [ ] **Acceptance:**
  - [ ] `useGroups.test.ts` — fetch/create/update/delete + member ops (add/remove/change-role)
  - [ ] `useExpenses.test.ts` — fetch (with reactive groupId) + create/update/delete + pagination/filtering
  - [ ] `useBalances.test.ts` — fetch balances/summary/stats + alias-mode normalization
  - [ ] `useUsers.test.ts` — admin CRUD + profile/password/stats/imports
  - [ ] `useInvitations.test.ts` — send/resend/revoke/validate/accept
  - [ ] `useAliases.test.ts` — CRUD + assign/remove/finalize
  - [ ] Each: happy path + error path (error → toast via stubbed `useNotifications`)
  - [ ] Extract a `mockUseApi()` helper only if 3+ tests repeat identical setup
- [ ] **Verify:** `pnpm test composables/resources/` green
- [ ] **Dependencies:** Task 7 (useApi mock pattern), Task 11 (template)
- [ ] **Files:** 6 new `.test.ts` files under `sd-frontend/app/composables/resources/`
- [ ] **Scope:** M (6 files, but each follows the established template)

### Task 13: Singleton + remaining composables
- [ ] **Acceptance:**
  - [ ] `useCategories.test.ts` — auto-fetch on first use + dedup via `fetchPromise`
  - [ ] `usePaymentModes.test.ts` — same singleton pattern
  - [ ] `use2FA.test.ts` — TOTP setup/verify/disable + backup codes
  - [ ] `useReceiptScan.test.ts` — image compression → POST → navigate with query params
  - [ ] `useUserSettings.test.ts` — theme/locale sync + debounced PUT + optimistic revert
  - [ ] `useAiStatus.test.ts` — fetch flag, silent on failure
- [ ] **Verify:** `pnpm test` green for all new files
- [ ] **Dependencies:** Task 7
- [ ] **Files:** 6 new `.test.ts` files
- [ ] **Scope:** M (6 files)

### Task 14: Add `frontend-unit-tests` job to `.gitlab-ci.yml`
- [ ] **Acceptance:**
  - [ ] New job `frontend-unit-tests` in `verify` stage
  - [ ] Script: `cd sd-frontend && pnpm install --frozen-lockfile && pnpm test`
  - [ ] Rules: same as existing `lint` / `typecheck` jobs (main, tags, MRs)
  - [ ] Job appears in CI pipeline for an MR
- [ ] **Verify:** Push an MR; confirm the job runs and passes
- [ ] **Dependencies:** Tasks 1-13 (suite must exist)
- [ ] **Files:** `.gitlab-ci.yml` (or the relevant `ci/*.yml` include)
- [ ] **Scope:** S (1 config file)

### Task 15: Document test rule in `sd-frontend/CLAUDE.md`
- [ ] **Acceptance:**
  - [ ] New "Testing" section in `sd-frontend/CLAUDE.md`
  - [ ] Documents: `pnpm test` / `pnpm test:watch` / `pnpm test -- <path>` commands
  - [ ] Documents: when to add tests (changing `app/utils/` or `app/composables/` logic)
  - [ ] Documents: colocation rule (`<file>.test.ts`)
  - [ ] Documents: mocking strategy (mock at boundaries, prefer real impls)
  - [ ] Documents: no E2E rationale + when to revisit
- [ ] **Verify:** Section reads clearly; matches spec boundaries
- [ ] **Dependencies:** Tasks 1-14
- [ ] **Files:** `sd-frontend/CLAUDE.md`
- [ ] **Scope:** S (1 file)

### Checkpoint: CI + docs
- [ ] CI runs `pnpm test` in verify stage
- [ ] CLAUDE.md documents the test rule
- [ ] **Review with human before proceeding**

## Phase 6: Selective component tests (P2) + contract (P3)

### Task 16: `ExpenseForm.test.ts` + `ImportMappingForm.test.ts`
- [ ] **Acceptance:**
  - [ ] `ExpenseForm` — mounts, renders fields; submit emits payload with splits; alias-mode renders alias splits; receipt scan query params populate fields
  - [ ] `ImportMappingForm` — mounts; mapping state advances; submit emits mapping payload
  - [ ] Both use `@vue/test-utils` `mount` with stubbed Nuxt UI children where needed
- [ ] **Verify:** `pnpm test` green for both
- [ ] **Dependencies:** Task 2 (component mounting setup)
- [ ] **Files:** `sd-frontend/app/components/expenses/ExpenseForm.test.ts`, `sd-frontend/app/components/ImportMappingForm.test.ts`
- [ ] **Scope:** M (2 files, component tests are heavier)

### Task 17: `ExpenseFilterCard.test.ts` + `DatePicker.test.ts` / `InputDate.test.ts`
- [ ] **Acceptance:**
  - [ ] `ExpenseFilterCard` — filter state changes emit expected events
  - [ ] `DatePicker` / `InputDate` — date parsing edge cases (invalid input, format)
- [ ] **Verify:** `pnpm test` green
- [ ] **Dependencies:** Task 2
- [ ] **Files:** 3 new `.test.ts` files
- [ ] **Scope:** S (3 small files)

### Task 18: Contract test — `gen:api` staleness check
- [ ] **Acceptance:**
  - [ ] `pnpm test:contract` script runs `openapi-typescript` and diffs against committed `app/types/api.d.ts`
  - [ ] Test fails if generated output differs from committed file (stale types)
  - [ ] Test passes when types are up-to-date
  - [ ] Wired into CI (separate job or part of `frontend-unit-tests` with a conditional)
- [ ] **Verify:** Modify `api.d.ts` by hand → `pnpm test:contract` fails; revert → passes
- [ ] **Dependencies:** Task 1 (script placeholder)
- [ ] **Files:** `sd-frontend/package.json` (fill script), `sd-frontend/scripts/gen-api-check.mjs` (or similar), `.gitlab-ci.yml`
- [ ] **Scope:** S (2-3 files)

### Checkpoint: Complete
- [ ] All acceptance criteria from `tasks/spec.md` met
- [ ] `pnpm test` < 10s, deterministic on repeated runs
- [ ] No Playwright, no browser binary
- [ ] Ready for review