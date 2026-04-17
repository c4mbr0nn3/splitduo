# Expense Split Precision Fix

## Overview

The expense form advertised 3-decimal precision on split amounts (`step="0.001"`), but the supporting code floored every intermediate value at 2 decimals, formatted currency at 2 decimals, and used inconsistent tolerances (0.001 in five places, 0.01 in one). The split-sum-equals-amount invariant could also break silently in several paths — a detached-object fallback in `splitByUser`, a `Math.max(0, …)` clamp in `distributeRemaining`, a stuck-at-zero branch in `updateSplits`, and full-reset behavior on every checkbox toggle or member-list diff.

This fix aligns the whole form (and downstream display components) on a consistent 3-decimal precision model, extracts currency math into a shared utility, replaces lossy 2-decimal rounding with integer-millicent arithmetic, and eliminates the silent-failure paths.

The backend was already compatible: `Expense.Amount` / `ExpenseSplit.SplitAmount` are `decimal` with no scale restriction, and `ExpensesService` uses a `0.001m` tolerance on both create and update (`SplitDuo.Core/Services/ExpensesService.cs:234, :494`). No backend changes were needed.

## Precision model

| Field               | Precision                        | Input step |
| ------------------- | -------------------------------- | ---------- |
| `expense.amount`    | 2 decimals                       | `0.01`     |
| `split.splitAmount` | 3 decimals                       | `0.001`    |
| Balance comparisons | Integer millicents (exact `===`) | —          |
| Backend tolerance   | `0.001m`                         | —          |

Splits use 3 decimals so division remainders (e.g., €10 / 3) can be absorbed exactly instead of rounded away.

## Findings & Solutions

### 1. Rounding truncated 3-decimal inputs to 2 decimals

**Impact**: €10 split three ways produced `[3.34, 3.33, 3.33]` instead of `[3.334, 3.333, 3.333]` — the split total fell 1¢ short and the form displayed a stale "Remaining: €0.01".

**Files**: `sd-frontend/app/components/expenses/ExpenseForm.vue:451, :474, :497, :599, :631`

**Fix**: Replaced all `Math.floor(x * 100) / 100` with integer-millicent helpers (`toMillis`, `fromMillis`, `splitMillis`, `rescaleMillis`). Every split calculation now operates in integer millicents and converts back only at assignment time, eliminating FP drift and preserving the 3rd decimal.

```js
const toMillis = (n) => Math.round((Number(n) || 0) * 1000);
const fromMillis = (m) => m / 1000;

const splitMillis = (totalMillis, n) => {
  if (n <= 0) return [];
  const base = Math.floor(totalMillis / n);
  const remainder = totalMillis - base * n;
  return Array.from({ length: n }, (_, i) => base + (i < remainder ? 1 : 0));
};
```

### 2. `formatCurrency` hid the 3rd decimal

**Impact**: A valid `€3.334` split rendered as `€3.33`, making the "Splits balanced" line look wrong even when splits summed exactly to the amount.

**Files**: `sd-frontend/app/components/expenses/ExpenseForm.vue:399–404` (inline formatter, now removed)

**Fix**: New `sd-frontend/app/utils/currency.js` exposes two auto-imported helpers — `formatCurrency` (symbol + number) and `formatAmount` (number only). Both render 2 decimals by default and only add the 3rd when it is non-zero, so normal 2-decimal values stay unchanged but 3-decimal splits are shown faithfully.

```js
const needs3 = Math.round(n * 1000) % 10 !== 0;
```

### 3. Tolerance drift across six call sites

**Impact**: `areSplitsEqual` accepted splits that differed by up to 1¢ — the "Split Equally" menu item hid when it should have been shown. Other comparisons used `0.001`, creating a 10× inconsistency.

**Files**: `sd-frontend/app/components/expenses/ExpenseForm.vue:209, :220, :359, :371, :493, :580, :623`

**Fix**: Eliminated float tolerances entirely. Since every split and amount round-trips through `toMillis` / `fromMillis`, balance comparisons are performed in integer-millicent space with exact `===` / `!==`. A `remainingMillis` computed sums per-split millis (never millis-of-sum) and drives the template, `showDistributeButton`, and form validation:

```js
const splitTotalMillis = computed(() =>
  (model.value.splits ?? [])
    .filter((s) => s.included)
    .reduce((t, s) => t + toMillis(s.splitAmount), 0),
);

const remainingMillis = computed(
  () => toMillis(model.value.amount) - splitTotalMillis.value,
);
```

`splitTotal` and `remainingAmount` are kept as thin `fromMillis(...)` projections, used only for user-facing `formatCurrency` output — never for comparisons. No magic epsilon, no FP drift.

### 4. `splitByUser` returned a detached object when entry was missing

**Impact**: `v-model` mutations on a member without an entry in `model.value.splits` were written to a throw-away object and silently lost — the checkbox would appear to toggle but state never persisted.

**Fix**: `splitByUser` now pushes a live entry into `model.value.splits` when missing, so every template binding touches the real reactive array.

```js
const splitByUser = (userId) => {
  if (!model.value.splits) model.value.splits = [];
  let s = model.value.splits.find((x) => x.userId === userId);
  if (!s) {
    s = { userId, included: false, splitAmount: 0 };
    model.value.splits.push(s);
  }
  return s;
};
```

### 5. `updateSplits` got stuck at zero when amount went 0 → X

**Impact**: In edit mode (or any flow where splits exist at 0 and member count already matches), setting the amount from 0 to a positive value left splits untouched — validation failed with no auto-fix.

**Fix**: After merging members into splits, `updateSplits` now detects `currentTotalMillis === 0 && amount > 0` and seeds an equal split before falling through to the proportional-rescale branch.

### 6. Member list changes wiped existing splits

**Impact**: When the member list length changed (member removed after expense created, or stale load in edit mode), the form fully re-initialized splits, destroying per-user values the user had already set.

**Fix**: `updateSplits` now merges by `userId` using a `Map`, preserving known entries and zero-filling newcomers. A `watch(groupMembers, …)` re-runs the merge whenever members load so edit-mode data is protected even when members arrive after the amount.

```js
const byId = new Map((model.value.splits || []).map((s) => [s.userId, s]));
model.value.splits = members.map(
  (m) =>
    byId.get(m.userId) ?? { userId: m.userId, included: true, splitAmount: 0 },
);
```

### 7. `handleSplitToggle` reset every custom split on any checkbox

**Impact**: A user who carefully set `[30, 70]` lost their proportions the moment they toggled a third member on or off.

**Fix**:

- **Toggle off**: zero the outgoing split, then `rescaleMillis` the remaining included splits to the full amount — ratios preserved.
- **Toggle on**: give the new member an equal share (`amount / n`), rescale the existing included splits to fit the remainder proportionally.

### 8. `distributeRemaining` silently clamped value away

**Impact**: `Math.max(0, split + adjustment)` turned negative outputs into zero and broke the invariant: split total no longer equaled amount even though the function claimed success.

**Fix**: Preflight the adjustments, abort with a toast when any recipient would go negative, and never clamp silently.

```js
const wouldGoNegative = targets.some(
  (s, i) => toMillis(s.splitAmount) + sign * adjustments[i] < 0,
);
if (wouldGoNegative) {
  showError("Cannot distribute: some splits would go negative");
  return;
}
```

### 9. Remainder always dumped on person #1

**Impact**: `roundedSplit * n` + single-person remainder gave the first member the entire rounding error (up to 1¢ at 2 decimals; up to 1 millicent at 3). Unfair and confusing in larger groups.

**Fix**: `splitMillis` spreads the remainder one millicent at a time across the first `remainder` recipients. Example: `€10 / 3` now yields `[3.334, 3.333, 3.333]` — 1-millicent premium on one seat instead of a 1¢ premium on one seat.

### Secondary cleanups

| Location                                     | Change                                                                                                             |
| -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ | --- | ------------------- |
| `ExpenseForm.vue:426`                        | Dropped redundant `!split.splitAmount                                                                              |     |`(covered by`<= 0`) |
| `ExpenseForm.vue:410` (`getSplitPercentage`) | Switched `Math.round` → `parseFloat(pct.toFixed(1))` so tiny splits show e.g. `0.3%` instead of collapsing to `0%` |
| `ExpenseForm.vue:399–404`                    | Removed inline `formatCurrency`; auto-imports the utility                                                          |

## Downstream display migration

All split/amount rendering now routes through `formatAmount(value)` from `sd-frontend/app/utils/currency.js` so 3-decimal values are never truncated after save.

| File                                                      | Lines migrated |
| --------------------------------------------------------- | -------------- |
| `sd-frontend/app/components/groups/ExpenseCard.vue`       | 24, 60         |
| `sd-frontend/app/components/groups/UserBalanceCard.vue`   | 26, 38, 46     |
| `sd-frontend/app/components/groups/MemberBalanceCard.vue` | 18, 28, 36     |
| `sd-frontend/app/components/dashboard/GroupCard.vue`      | 49, 50         |
| `sd-frontend/app/components/groups/StatsCards.vue`        | 35             |
| `sd-frontend/app/components/dashboard/StatCard.vue`       | 62             |

2-decimal values still render as `€12.50` (unchanged); only genuine 3-decimal values pick up the third digit.

## Verification

1. **`pnpm dev`** in `sd-frontend/` and log in as `admin@splitduo.local`.
2. **Fair distribution**: create an expense for €10 split across 3 members → splits `[3.334, 3.333, 3.333]`, total exactly €10, "✓ Splits balanced" shown.
3. **No-person-bias**: €100 / 7 → six members at `14.285`, one at `14.290` (single-millicent premium, not a cent).
4. **Toggle preserves ratios**: set `[30, 40, 30]` for €100, toggle one off → remaining two become `[42.857, 57.143]` (not reset to `[50, 50]`).
5. **Amount transition 0 → X**: open an expense, clear the amount, re-enter it — splits auto-populate equally instead of staying at 0.
6. **Member-list change**: open an expense where a former member was removed from the group — remaining members keep their original splits, removed member's entry is dropped.
7. **Negative-distribute guard**: reduce a split below 0 by "Distribute Remaining" → error toast, splits untouched.
8. **Display consistency**: save a 3-decimal split, inspect `ExpenseCard`, `UserBalanceCard`, `MemberBalanceCard`, dashboard `GroupCard`, `StatsCards`, `StatCard` — 3rd decimal shown where meaningful, ordinary 2-decimal values unchanged.
9. **Lint**: `pnpm lint:fix` reports 0 issues.
10. **Backend round-trip**: save a 3-decimal split, reload the expense — values persist unchanged.
