# SplitDuo Frontend UI Consistency Fix Plan

**Derived from:** `/home/j1mm0/Workspace/splitduo/UI-DESIGN-REVIEW.md`  
**Goal:** Make the UI consistent, mobile-first, modern, and friendly without redesigning the app.  
**Authority:** This plan is the canonical design decision document. Implementers (@fixer) must follow the project-wide rules below exactly.

---

## 1. Project-Wide Design Rules

These rules will be mirrored into `sd-frontend/CLAUDE.md` and must be applied in every lane.

### Page vertical rhythm
- Default pages: `py-6 px-4` on mobile, `sm:py-8` on `sm:` and up.
- Auth pages: centered vertically, no `py-*` on the page wrapper; use `p-4`.
- Never mix `py-4`, `py-6`, `py-8`, or `p-4` page-level padding arbitrarily.

### Auth page layout
- Page wrapper: `min-h-dvh flex items-center justify-center p-4` (replace all `h-screen` and `min-h-[80vh]`).
- Card: `UCard class="w-full max-w-md"`.
- Auth forms are the only place where a narrow `max-w-md` is correct.

### Page headers / titles
- Use the existing `UiCardHeader` primitive for all page and card headers.
- Page-level title: `text-2xl font-bold text-primary` (set inside `UiCardHeader` for pages that need a title).
- Card-level title inside a `UCard #header`: `text-lg font-semibold`.
- Dashboard and similar "titleless" pages get a `UiCardHeader` at the top with `title="Dashboard"` and no `subtitle`.

### Button conventions
- **Primary CTA:** default `UButton` (solid primary, teal). Exception: destructive primary actions use `color="error"`.
- **Secondary action:** `variant="outline"` or `variant="ghost"` with `color="neutral"`.
- **Destructive action:** `color="error" variant="soft"` when it is a labeled action; `color="error" variant="ghost"` when icon-only.
- **Icon-only action buttons:** `size="sm" square`. Minimum gap between adjacent icon-only actions: `gap-2` (prefer `gap-3` for destructive neighbors).
- **Full-width buttons on mobile:** when a single CTA sits at the bottom of a form or card, wrap it with `w-full sm:w-auto` only where a narrow button breaks the layout.

### Form containers
- Complex forms (group, expense, user, profile): `UCard class="w-full max-w-2xl"`, centered with `flex flex-col items-center`.
- Auth forms: `UCard class="w-full max-w-md"`.
- Do not mix `max-w-lg`, `max-w-md`, `max-w-2xl` without reason.

### Semantic colors
- Use Nuxt UI semantic colors only: `primary`, `secondary`, `success`, `error`, `warning`, `info`, `neutral`.
- **Never** use raw Tailwind utilities such as `text-green-600`, `text-red-600`, `bg-green-100`, `border-gray-200`, `dark:border-gray-700`.
- Use `text-success` / `text-error` / `text-warning` / `text-info` / `text-muted` / `text-dimmed` from Nuxt UI tokens for foreground colors.
- Use `border-muted` / `bg-muted/10` / `bg-muted/30` for neutral surfaces and borders.

### Icon set
- **Lucide only.** All icons use `i-lucide-*`.
- Heroicons are only allowed when a Lucide equivalent truly does not exist; currently no such case exists.

### Search input responsive width
- Search inputs on list pages: `class="w-full sm:w-64 md:w-80"`.
- This makes the input full-width on mobile and constrained on larger screens.

### Empty states
- `UiEmptyState` is used bare on the page/card body background.
- If it must sit inside a `UCard`, the card uses `variant="ghost"` or `variant="soft"`, **never** `variant="outline"`.

### Toast notifications
- `useNotifications.js` must pass `duration: 4000` to every toast.
- Mobile: `position: 'top-center'` (avoids covering bottom nav and input fields).
- Desktop: `position: 'bottom-right'`.
- Use `color: 'success' | 'error' | 'warning' | 'info'` as today.

### Page-level loading / error / retry
- Loading: `UiLoadingSpinner` centered in the page/card body.
- Error: `UiEmptyState` with an action slot containing a retry `UButton`.
- Retry button: `color="primary" variant="outline" size="sm"`.
- Apply to any page that currently shows nothing while data loads/fails (group detail, members, invite, admin user edit, group detail index).

### Safe-area / PWA bottom affordances
- Default layout main content must include `pb-[env(safe-area-inset-bottom)]` (or equivalent Tailwind/Nuxt UI token if available, otherwise a CSS custom property) so content is not hidden by the iOS home indicator.
- No bottom tab bar is introduced.

### Mobile FAB decision
- **No global FAB.** Keep navigation inside the existing `UHeader` drawer.
- The group expenses tab already has a sticky "Add Expense" pattern via the filter bar; leave it as is, but ensure the button touch target is at least `size="sm"`.

---

## 2. Implementation Lanes

All lanes are read/write only on the files listed. Lanes are designed to be parallelizable except where noted.

### Lane A: Auth pages
**Scope:** Unify login, verify, forgot-password, reset-password, invite-accept layouts and mobile sizing.

**Files owned:**
- `app/pages/index.vue`
- `app/pages/auth/verify.vue`
- `app/pages/forgot-password.vue`
- `app/pages/reset-password.vue`
- `app/pages/invite/accept.vue`

**Tasks (in order):**
1. `pages/index.vue:2` — replace `h-screen` with `min-h-dvh`. Replace inner wrapper `class="w-full sm:w-3/4 md:w-1/2 space-y-4"` with `class="w-full max-w-md space-y-4"`.
2. `pages/auth/verify.vue:2` — same as above.
3. `pages/auth/verify.vue` — make verify button and backup code input/button full-width (`block w-full`) inside the tab panels so both tabs match.
4. `pages/forgot-password.vue:56` — replace `min-h-[80vh]` with `min-h-dvh`; ensure outer wrapper is `flex items-center justify-center p-4`.
5. `pages/reset-password.vue:121` — same as forgot-password.
6. `pages/invite/accept.vue:126` — same as forgot-password.
7. Verify all auth cards use `UCard class="w-full max-w-md"`.
8. In `invite/accept.vue`, replace raw `text-success` / `text-muted` in the password rule checklist with semantic tokens (`text-success`, `text-muted` are actually the token names; confirm they resolve through Nuxt UI and do not use raw `text-green-*`). If they are already semantic, leave them.

**Design rules cited:** Auth page layout, Page vertical rhythm, Button conventions, Semantic colors.

**Verification:** Run `pnpm lint:fix`. Visually check each auth page in a 375px mobile viewport and desktop viewport.

---

### Lane B: Shared primitives + CLAUDE.md rules + notifications + layout safe-area
**Scope:** Update the durable rules in CLAUDE.md, fix toast behavior, and add safe-area padding.

**Files owned:**
- `sd-frontend/CLAUDE.md`
- `app/composables/utils/useNotifications.js`
- `app/layouts/default.vue`
- `app/components/ui/EmptyState.vue`
- `app/components/ui/CardHeader.vue` (read, maybe extend)

**Tasks (in order):**
1. Edit `sd-frontend/CLAUDE.md` — insert a new `## UI Design Rules` section immediately after `## Styling` containing the Project-Wide Design Rules above (concise bullet style, exact tokens).
2. `app/composables/utils/useNotifications.js` — add `duration: 4000` and `position: 'top-center'` to every `toast.add()` call. On desktop detection is optional; if implementing a single position, use `'top-center'` universally because it works on both mobile and desktop and is safer than bottom-right on mobile.
3. `app/layouts/default.vue` — add `pb-[env(safe-area-inset-bottom)]` to `UMain` (or to the wrapping `div`) so page content clears the iOS home indicator.
4. `app/components/ui/EmptyState.vue` — ensure title uses a semantic color token (`text-dimmed` is already a token, keep; subtitle uses `text-muted`, keep). No code change likely needed, but confirm.
5. `app/components/ui/CardHeader.vue` — ensure default title style is `text-xl font-bold text-primary` and the page-style variant is `text-2xl font-bold text-primary`. If the component cannot distinguish page vs card headers, add a `size` prop with values `'md'` (card, default) and `'lg'` (page). Update callers in later lanes if this prop is added.

**Design rules cited:** Toast duration & position, Safe-area padding, Page headers / titles, Empty state usage.

**Verification:** Run `pnpm lint:fix`. Inspect `useNotifications` toast behavior manually. Check `default.vue` renders without horizontal overflow on mobile.

---

### Lane C: Lists — groups + admin users + search + dashboard quick-actions
**Scope:** Make list/search pages consistent and mobile-friendly.

**Files owned:**
- `app/pages/groups/index.vue`
- `app/pages/admin/users/index.vue`
- `app/pages/dashboard.vue`
- `app/components/dashboard/StatCard.vue`

**Tasks (in order):**
1. `pages/groups/index.vue:4` — add a `UiCardHeader title="Groups" subtitle="Manage your expense sharing groups"` at the top inside the `py-6`/`py-8` wrapper.
2. `pages/groups/index.vue:14` — change search `class="w-64"` to `class="w-full sm:w-64 md:w-80"`.
3. `pages/groups/index.vue:27` — change create `+` button to default primary (remove `color="success"`, keep `variant="outline"` if it must be a secondary-looking action; per rules, a primary CTA should be default/solid). Decision: make it a primary solid `UButton` with `icon="i-lucide-plus"` and `label="Create"` on `sm:` only (`hidden sm:inline`).
4. `pages/groups/index.vue:98-110` — change edit/delete icon buttons to `size="sm" square` and increase gap to `gap-2` minimum (`gap-3` preferred).
5. `pages/groups/index.vue:128-132` — replace hardcoded euro string `€${...}` with `formatCurrency(...)` and keep the badge.
6. `pages/admin/users/index.vue:4` — add `UiCardHeader title="Users" subtitle="Manage platform users"`.
7. `pages/admin/users/index.vue:44` — change search `class="w-64"` to `class="w-full sm:w-64 md:w-80"`.
8. `pages/admin/users/index.vue:50` — refresh button already `variant="ghost" square`; confirm it is `size="sm"` minimum.
9. `pages/admin/users/index.vue:106-142` — if any action buttons in pending invitations are `size="xs"`, change to `size="sm" square` and `gap-2` minimum.
10. `pages/dashboard.vue` — add `UiCardHeader title="Dashboard"` at the top. Change "View All" button to `size="sm" variant="outline" color="neutral"`. Change quick-action buttons to `size="sm"` and keep `block justify-start`. Swap "You Owe" icon `i-lucide-frown` to `i-lucide-trending-down` and "You're Owed" to `i-lucide-trending-up` for a neutral finance feel.
11. `components/dashboard/StatCard.vue` — remove raw Tailwind color classes in the `default` case (`text-white`, `bg-gray-100`, `border-gray-600`) and map them to semantic tokens. Keep the explicit `teal`/`green`/`red`/`rose`/etc. mappings because they accept named props; ensure each maps to a Nuxt UI semantic class. Replace the fallback `text-white` with `text-primary`.

**Design rules cited:** Page headers / titles, Search input responsive width, Button conventions, Semantic colors, Icon set.

**Verification:** Run `pnpm lint:fix`. Check groups list and admin users list on 375px width; verify search stretches full width and action buttons are tappable.

**Dependencies:** `UiCardHeader` prop extension from Lane B if adopted.

---

### Lane D: Group detail + members + invite + expense card
**Scope:** Fix the group detail family of pages and shared cards.

**Files owned:**
- `app/pages/groups/[id]/index.vue`
- `app/pages/groups/[id]/members.vue`
- `app/pages/groups/[id]/invite.vue`
- `app/components/groups/SectionHeader.vue`
- `app/components/groups/ActionsDropdown.vue`
- `app/components/groups/ExpenseCard.vue`
- `app/components/groups/UserBalanceCard.vue`
- `app/components/groups/MemberBalanceCard.vue`
- `app/components/groups/members/Card.vue`

**Tasks (in order):**
1. `pages/groups/[id]/index.vue` — add `UiLoadingSpinner` while `currentGroup` is null and an error `UiEmptyState` with retry if loading fails. Use `UiEmptyState icon="i-lucide-users" title="Unable to load group"`.
2. `pages/groups/[id]/members.vue` — add loading/error states for members; pending-invitation resend/revoke buttons to `size="sm" square` with `gap-3`. Footer "Invite User" button: `w-full sm:w-auto`.
3. `pages/groups/[id]/invite.vue` — wrap `GroupsInviteUsersForm` in a `UCard variant="soft"` body if it is currently bare, so the form section matches other forms. Or, if the page-level card is enough, leave as is but document the decision in the lane PR.
4. `components/groups/SectionHeader.vue` — delete icon `UButton` already `size="sm"`; ensure destructive is `color="error" variant="soft"` (it is). Confirm member-count button is `size="sm"`.
5. `components/groups/ActionsDropdown.vue` — change `i-heroicons-ellipsis-vertical` to `i-lucide-ellipsis-vertical`.
6. `components/groups/ExpenseCard.vue` — change `size="sm"` icon-only edit/delete buttons to `square` and `gap-2`. Replace raw `text-green-600` / `text-red-600` amount colors with Nuxt UI semantic classes. Use `text-success` when paid by current user and `text-error` when not (or keep the semantic meaning). Verify `formatCurrency` is used for the amount.
7. `components/groups/UserBalanceCard.vue` — replace raw `text-green-600`, `text-red-600`, `bg-green-100`, `bg-red-100`, `text-orange-500` with semantic tokens (`text-success`, `text-error`, `bg-success/10`, `bg-error/10`, `text-warning`).
8. `components/groups/MemberBalanceCard.vue` — same color-token cleanup as UserBalanceCard.
9. `components/groups/members/Card.vue` — ensure role badge uses `color="primary"` for admin and `color="secondary"` for member; already close, verify.

**Design rules cited:** Page-level loading/error, Button conventions, Semantic colors, Icon set.

**Verification:** Run `pnpm lint:fix`. Check group detail on mobile; verify member/invite action buttons are tappable and balance colors still make sense in dark mode.

---

### Lane E: Forms — expense form + group form + user form + imports + 2FA + profile
**Scope:** Standardize form layout, button variants, and semantic colors across all forms and settings/profile pages.

**Files owned:**
- `app/components/expenses/ExpenseForm.vue`
- `app/components/groups/GroupForm.vue`
- `app/components/admin/UserForm.vue`
- `app/components/ChangePasswordModal.vue`
- `app/components/ImportMappingForm.vue`
- `app/components/ImportAnalysisResults.vue`
- `app/pages/groups/[id]/imports/add.vue`
- `app/pages/groups/[id]/imports/index.vue`
- `app/pages/settings/2fa/setup.vue`
- `app/pages/profile.vue`

**Tasks (in order):**
1. `components/groups/GroupForm.vue` — submit button: default primary solid (`variant="subtle"` → remove variant). Back button: `variant="outline" color="neutral"`. Ensure all inputs `size="lg"`.
2. `components/admin/UserForm.vue` — same as GroupForm.
3. `components/expenses/ExpenseForm.vue` — split-button "Add More" pair: make both buttons the same variant family. Decision: primary action `variant="subtle"`, dropdown chevron `variant="outline"` is current; change both to `variant="outline"` OR both to `variant="subtle"`. Choose `variant="subtle"` for both for visual consistency. Change Heroicons in split menu (`i-heroicons-arrows-right-left`, `i-heroicons-equals`) to Lucide (`i-lucide-arrow-left-right`, `i-lucide-equal`).
4. `components/ChangePasswordModal.vue` — ensure cancel is `variant="outline" color="neutral"` and confirm is default primary. Form fields already `class="w-full"`; verify.
5. `components/ImportMappingForm.vue` — replace `border-gray-200 dark:border-gray-700` with `border-muted`. Ensure submit button is default primary.
6. `components/ImportAnalysisResults.vue` — same border-token cleanup. No other changes.
7. `pages/groups/[id]/imports/add.vue` — remove outer `UCard` body padding redundancy when `currentStep === 'analysis'` by rendering `ImportAnalysisResults` without the extra wrapping card (it already provides its own card). Make the step indicator horizontally scrollable: wrap it in `div class="overflow-x-auto pb-2"`.
8. `pages/groups/[id]/imports/index.vue` — remove the outer `UCard variant="soft"` or apply `variant="soft"` consistently to all group detail sub-pages. Decision: remove `variant="soft"` to match the other detail pages.
9. `pages/settings/2fa/setup.vue` — replace page wrapper with default page rhythm (`py-6 px-4 sm:py-8` instead of `max-w-lg mx-auto space-y-6 p-4`). Wrap content in a `UCard class="w-full max-w-2xl"` centered, or keep cards but align width with other pages. Standardize each card to have a `#header`. QR code container: use `bg-white dark:bg-white rounded-lg` to preserve QR contrast in both modes.
10. `pages/profile.vue` — 2FA status row: consider extracting to a `UiStatusRow` primitive in `components/ui/`. If extracted, create `components/ui/StatusRow.vue` and use it here and in 2FA setup. If too heavy, leave inline but ensure colors are semantic. Retry button: `w-full sm:w-auto`.

**Design rules cited:** Form containers, Button conventions, Semantic colors, Page vertical rhythm, Page headers / titles, Page-level loading/error, Empty state usage.

**Verification:** Run `pnpm lint:fix`. Check forms on 375px viewport; verify import step indicator scrolls and 2FA setup looks consistent.

**Dependencies:** If Lane B adds a `size` prop to `UiCardHeader`, update any header in this lane using the new prop.

---

### Lane F: Layout + header + notifications + misc cleanup
**Scope:** Header consistency, icon-set cleanup in ButtonDropdown, and final safe-area/layout touches.

**Files owned:**
- `app/components/layout/AppHeader.vue`
- `app/components/ui/ButtonDropdown.vue`
- `app/components/PwaUpdate.vue`
- `app/layouts/auth.vue` (read, maybe no change)
- `app/components/ui/DatePicker.vue` (dead code note, no change unless needed)

**Tasks (in order):**
1. `app/components/layout/AppHeader.vue` — confirm color-mode button and logout button use `variant="ghost" color="neutral"` (already close). Ensure the drawer `ButtonColorMode` uses the same styling.
2. `app/components/ui/ButtonDropdown.vue` — change default `dropdown-icon="i-heroicons-chevron-down"` to `i-lucide-chevron-down`.
3. `app/components/PwaUpdate.vue` — the FAB uses `size="lg"` and `fixed bottom-6 right-6`. Keep, but add `mb-[env(safe-area-inset-bottom)] mr-[env(safe-area-inset-right)]` or wrap with `safe-area-inset-bottom` equivalent so it clears the home indicator. If Tailwind v4 does not expose `safe` utilities, use arbitrary values: `bottom-[calc(1.5rem+env(safe-area-inset-bottom))]`.
4. `app/components/ui/DatePicker.vue` — leave unused but note in PR that it is dead code. Do not delete per instructions.

**Design rules cited:** Icon set, Safe-area padding, Button conventions.

**Verification:** Run `pnpm lint:fix`. Verify header drawer opens/closes and the PWA update button position is safe-area aware.

---

## 3. Sequencing & Dependencies

- **Lane B (shared primitives + CLAUDE.md) should land first** if it changes `UiCardHeader` by adding a `size` prop. Lanes C, D, and E depend on that prop if they adopt it.
- If `UiCardHeader` is **not** modified (only used as-is), all lanes are fully parallel except:
  - Lane A can go first or parallel — no dependencies.
  - Lane F has no dependencies and can run in parallel.
- Recommended order if a single implementer wants minimal conflicts:
  1. Lane B
  2. Lane A
  3. Lane C
  4. Lane D
  5. Lane E
  6. Lane F
- For multiple parallel @fixer instances: run **Lane B first**, then C, D, E, and F in parallel; Lane A can run in parallel with B.

---

## 4. Risk Notes

- **`h-screen` → `min-h-dvh`**: improves iOS Safari but may change how desktop browsers render very short viewports. The centered layout is safe.
- **Button variants**: switching primary actions from `variant="subtle"` to default solid may look more prominent. This is intended. If any E2E tests assert class names, they may need updating.
- **Semantic color tokens**: replacing raw Tailwind colors with Nuxt UI tokens should preserve visual intent in light mode and improve dark mode. Verify dark mode manually because some raw classes (`bg-white` for QR code) are intentionally kept for contrast.
- **Search `w-full` on mobile**: will stretch the input to the edge of `UContainer`; ensure `UContainer` padding is not collapsed.
- **Toast position**: changing to `top-center` will move notifications. This is intentional for mobile safety.
- **Safe-area padding**: using `env(safe-area-inset-bottom)` requires iOS Simulator or a real device to verify; on desktop it evaluates to `0px` and is harmless.

---

## 5. Out of Scope

- No backend changes.
- No new features (e.g., no real-time updates, no new pages, no new data flows).
- No complete redesign or new animations.
- No deletion of dead code (e.g., `DatePicker.vue`, unused icon sets) — only notes in PRs.
- No TypeScript or Pinia introduction.

---

## 6. Verification

This plan is written to `/home/j1mm0/Workspace/splitduo/UI-FIX-PLAN.md`.  
It contains: Project-Wide Design Rules, Implementation Lanes, Sequencing & Dependencies, Risk Notes, Out of Scope.  
After each lane: run `pnpm lint:fix` and do a mobile-viewport visual check.  
After all lanes: compare key pages side-by-side for padding, header sizes, button variants, and color tokens.
