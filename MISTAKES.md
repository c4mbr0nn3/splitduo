# MISTAKES.md

Log of wrong fixes, misdiagnoses, and hallucinations (what happened, root cause, prevention).
When the same root cause appears 3+ times, graduate it into AGENTS.md / CLAUDE.md as a hard rule.

## 2026-08-31 — Plan bug: composable instantiated inside event handler

**What happened:** The settle-up modal plan instructed instantiating `useBalances(groupId)` inside the click handler. At runtime every group click failed with "Couldn't load settlement suggestions" — no API request ever fired.

**Root cause:** `useBalances` (and most resource composables) call `useI18n()`/`useApi()`/`useNotifications()` at creation, which require Vue component setup context and throw when called inside an event handler. The modal's catch swallowed the setup-context error and surfaced a misleading "load failed" message. Misdiagnosis cost: two investigation rounds chased backend/cache/test-data theories (stale HybridCache, wrong logged-in user, hand-edited DB) before a live network capture showed zero requests — proving a synchronous client-side throw.

**Prevention:**
- Never instantiate composables that depend on setup context (i18n, api, notifications, NuxtApp) outside `setup()`. If a composable needs a runtime-determined id, instantiate it at setup top level with a reactive ref and set the ref at event time.
- When debugging "request failed" errors, check the network log for the absent request first — a zero-request failure means the throw happened before the call, ruling out the backend immediately.
- Plans that mandate non-standard patterns ("instantiate inside the handler") deserve a sanity check against framework rules before handoff to an executor.

## 2026-08-31 — Plan bug: nonexistent `#label` slot on Nuxt UI v4 UButton

**What happened:** The badge plan specified putting group name + badge inside `UButton`'s `#label` slot. Vue silently discards templates for nonexistent slots, so all five group rows rendered as empty buttons. `pnpm typecheck` passed — slot typing was too loose to catch it.

**Root cause:** Second instance of the same underlying cause as the entry above: an unverified assumption about a library API reached the plan and the executor followed it faithfully. UButton in Nuxt UI v4 has only `leading`, default, and `trailing` slots. The bug was only caught by live browser verification (empty accessible names in the a11y snapshot).

**Prevention:**
- Verify slot/prop names against the installed component source (`node_modules/@nuxt/ui/dist/runtime/components/*.vue`) before specifying them in a plan — docs may lag the installed version.
- Typecheck passing does not prove template correctness: slot names and dynamic template structure need runtime/browser verification for UI changes.
- Any UI change gets at least one live browser check before commit (a11y snapshot or DOM query), even when lint/typecheck/tests are green.