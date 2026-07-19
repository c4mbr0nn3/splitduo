# SplitDuo Frontend UI/UX Design Review

**Scope:** `sd-frontend/app/pages/`, `app/layouts/`, `app/components/`  
**Date:** 2026-07-19  
**Reviewer note:** This is a read-only review. No source files were modified.

---

## Summary

The SplitDuo frontend is a clean, modern Nuxt 4 + Nuxt UI v4 app with a consistent teal/zinc/rose color palette, a readable Geist typeface, and sensible mobile-first defaults. Most pages use Nuxt UI primitives correctly, and the app already avoids custom styled wrappers in favor of utility classes. However, the UI is held back by **fragmented consistency**: page-level spacing, form layouts, button sizing/color, empty states, and header/heading styles vary across pages. On mobile, several pages use desktop-centric sizing (`h-screen`, `max-w-md` without full width on small screens, `w-64` search inputs) and tiny controls (`size="xs"`, `size="sm"`) that can make touch targets and readability weaker than intended. The good news is that most issues are small, repeatable fixes rather than structural problems.

---

## Strengths

- **Cohesive palette:** Primary teal + secondary rose + neutral zinc is applied consistently in `app.config.ts` and `main.css`.
- **Nuxt UI adoption:** Most forms, cards, buttons, badges, tabs, modals, toasts, and inputs use Nuxt UI components rather than custom HTML.
- **Mobile-first container:** Default layout wraps content in `<UContainer>` inside `<UMain>`, giving the app a contained, app-like feel on phones.
- **Skeleton states:** Dashboard, groups list, admin users, and expenses list all show skeletons while loading.
- **Reusable primitives:** `UiEmptyState`, `UiCardHeader`, `UiLoadingSpinner`, `UiButtonDropdown`, `useModal`, and `useNotifications` centralize common patterns.
- **Dark mode support:** Uses Nuxt UI color mode with the custom `<ButtonColorMode>` toggle and `dark:` overrides in `main.css`.
- **Accessibility touches:** Most form inputs use `UFormField`, buttons have `aria-label`, and the 2FA pin input uses `autocomplete="one-time-code"`.

---

## Cross-Cutting Issues

| Issue | Impact | Recommendation | Affected files |
|-------|--------|----------------|----------------|
| **Inconsistent page padding/spacing** | Some pages use `py-8`, some `py-6`, some `p-4`, and auth pages use none; makes the app feel jumpy when navigating. | Pick one page vertical rhythm (e.g., `py-6` on mobile, `py-8` on `sm:`) and apply everywhere except centered auth pages. | `pages/dashboard.vue`, `pages/groups/index.vue`, `pages/groups/[id]/*.vue`, `pages/settings/2fa/setup.vue`, `components/*/Form.vue` |
| **Mixed heading hierarchy / title styling** | Page titles appear as `text-2xl font-bold text-primary`, `text-xl font-semibold text-primary`, or `text-xl font-bold text-primary` depending on the page. | Standardize on one title component/style. `UiCardHeader` already exists but is not used everywhere (e.g., dashboard has inline headers). | `pages/dashboard.vue:41`, `pages/groups/index.vue:4`, `pages/admin/users/index.vue:4`, `pages/groups/[id]/imports/add.vue:9`, `components/ui/CardHeader.vue:17` |
| **Button color/variant inconsistency** | Primary actions use `variant="subtle"`, `variant="solid"`, or default depending on the page; destructive uses `error` mostly but is sometimes raw red (`text-red-500`). | Set a project-wide rule: primary CTA = default `UButton` (solid primary) or `variant="subtle"` consistently; destructive = `color="error" variant="soft"` for emphasis, `variant="ghost"` for icon-only actions. | `components/groups/GroupForm.vue:48`, `components/admin/UserForm.vue:74`, `components/expenses/ExpenseForm.vue:239`, `pages/dashboard.vue:46`, `pages/groups/index.vue:21` |
| **Tiny touch targets** | Several action buttons use `size="xs"` (`w-6 h-6` area) or small icon-only buttons packed tightly. On mobile, these are hard to tap and too close to neighbors. | Increase icon-only action buttons to at least `size="sm"` and add `square` + a minimum 10px gap between destructive/primary actions. | `pages/groups/[id]/members.vue:59`, `pages/admin/users/index.vue:50`, `pages/groups/[id]/imports/index.vue:94`, `components/groups/ExpenseCard.vue:81`, `components/admin/UserCard.vue:29` |
| **Search inputs are not full-width on mobile** | `groups/index.vue` and `admin/users/index.vue` set `class="w-64"` on the search input. On small screens this leaves dead space and truncates placeholder text. | Use `class="w-full sm:w-64 md:w-80"` so the search bar stretches on mobile. | `pages/groups/index.vue:18`, `pages/admin/users/index.vue:48` |
| **Inconsistent form width/centering** | Some forms center with `max-w-2xl`, some with `max-w-md`; the 2FA setup uses `max-w-lg mx-auto`; invite/reset/forgot use `max-w-md` but only `w-full` inside `min-h-[80vh]`. | Decide one standard form container (e.g., `UCard class="w-full max-w-2xl"` for complex forms, `max-w-md` for auth) and reuse it. | `pages/invite/accept.vue:127`, `pages/reset-password.vue:122`, `pages/forgot-password.vue:57`, `components/groups/GroupForm.vue:3`, `components/admin/UserForm.vue:3`, `pages/settings/2fa/setup.vue:2` |
| **Mixed color tokens for semantic meaning** | Money is shown with raw `text-green-600`/`text-red-600` in some components and Nuxt UI `color="success"`/`color="error"` in others; `DashboardStatCard` maps `rose` but the app never uses `rose` anywhere else. | Use Nuxt UI semantic colors (`success`, `error`, `primary`, `warning`, `neutral`) consistently instead of Tailwind color utilities. | `components/groups/UserBalanceCard.vue`, `components/groups/MemberBalanceCard.vue`, `components/groups/ExpenseCard.vue:22`, `components/dashboard/StatCard.vue:67` |
| **Icon set inconsistency** | Most icons are Lucide (`i-lucide-*`), but a few places still use Heroicons (`i-heroicons-ellipsis-vertical`, `i-heroicons-arrows-right-left`, `i-heroicons-equals`). | Switch all icons to Lucide for consistency, or explicitly decide Lucide for UI and Heroicons only where a Lucide equivalent is missing. | `components/groups/ActionsDropdown.vue:6`, `components/expenses/ExpenseForm.vue:400`, `components/ui/ButtonDropdown.vue:65` |
| **Empty-state styling drifts** | `UiEmptyState` centers content but uses `text-dimmed` for the title and `text-muted` for subtitle; some inline empty states (e.g., group summary) wrap `UiEmptyState` in `UCard variant="outline"`, while others use it bare. | Keep `UiEmptyState` bare on a neutral background; add `variant="ghost"` or `variant="soft"` to the parent card only when it sits inside another card. | `components/ui/EmptyState.vue:7`, `components/groups/ExpensesTab.vue:14`, `components/groups/StatsTab.vue:40` |
| **Toast notifications lack duration/position standardization** | `useNotifications` does not set `duration` or `position`; defaults may differ per platform and toast stacking can feel abrupt on mobile. | Add `duration: 4000` and `position: 'top-center'` (or bottom-center on mobile) to all toasts in `useNotifications`. | `app/composables/utils/useNotifications.js` |
| **Raw color utilities in mapping/analytics components** | `ImportMappingForm` and `ImportAnalysisResults` use raw `border-gray-200 dark:border-gray-700` and `text-primary` instead of Nuxt UI semantic tokens (`border-muted`, `text-primary` is fine but the gray border should be `border-muted`). | Replace hard-coded gray borders with `border-muted` or `border-default` so dark mode stays consistent. | `components/ImportMappingForm.vue:34`, `components/ImportAnalysisResults.vue:72` |
| **No visible page-level loading/error state on many pages** | Beyond skeletons, pages like `groups/[id]/index.vue` do not show a loader or error if `fetchGroup` fails. | Add a centered `UiLoadingSpinner` while `currentGroup` is null and a retry `UiEmptyState` if loading fails. | `pages/groups/[id]/index.vue`, `pages/groups/[id]/invite.vue`, `pages/groups/[id]/members.vue` |

---

## Per-Page Findings

### `pages/index.vue` (Login)

- `h-screen p-4` centers the card but on short landscape phones the card can be clipped; prefer `min-h-dvh` plus `items-center p-4`.
- Email/password fields use `size="lg"`, which is good for mobile thumb typing.
- The submit button relies on `UAuthForm`’s internal rendering and is not directly visible in this file; verify the rendered button is full-width on mobile.
- `Forgot your password?` link uses `text-muted hover:text-primary` — consistent and readable.

**Recommendation:** Replace `h-screen` with `min-h-dvh` and ensure the inner wrapper is `w-full max-w-sm` (currently `sm:w-3/4 md:w-1/2` becomes small on phones).

### `pages/auth/verify.vue`

- Same `h-screen` issue as login.
- The `UPinInput` with `type="number"` may show a numeric keyboard, but `otp` + `autocomplete="one-time-code"` is well done.
- Verify button is not full-width; it uses `block` inside a centered column. On narrow screens it may be too narrow for a thumb tap.
- Backup code tab uses a plain `UInput` that is not full-width inside the column.

**Recommendation:** Make the TOTP verify button `block`/`w-full` (it already has `block`), and make backup `UInput` + button full-width so both tabs match.

### `pages/forgot-password.vue` / `pages/reset-password.vue` / `pages/invite/accept.vue`

- All three use `min-h-[80vh] items-center justify-center` with `UCard class="w-full max-w-md"`. This is inconsistent with login/verify which use `h-screen` and `sm:w-3/4 md:w-1/2`.
- Success/invalid states reuse the same centered icon + heading + subtitle pattern — good consistency within this trio.
- Password forms include live rule checklists, which is excellent UX; however, the checklist text in `invite/accept.vue` uses raw `text-success`/`text-muted` rather than semantic Nuxt UI colors.
- `reset-password.vue:94` uses `setTimeout` for navigation — fine functionally, but the "Redirecting..." message should ideally show a progress indicator.

**Recommendation:** Unify auth layout sizing: use `min-h-dvh items-center justify-center p-4` and a shared wrapper width (`w-full max-w-md`) for all auth pages.

### `pages/dashboard.vue`

- Page uses `py-8` but `dashboard.vue` is the only page that does not start with a `UiCardHeader` or page title, relying instead on card headers (`Recent Groups`, `Quick Actions`).
- Stat cards use raw `red`/`green`/`teal` props; the "You Owe" card icon `i-lucide-frown` feels slightly judgmental — a neutral money icon would be friendlier.
- "View All" button on the groups card uses `variant="subtle" size="sm"`, while the dashboard quick-action buttons use `size="lg" block`. The two cards therefore feel visually unrelated.
- Quick action icons are left-aligned with `class="justify-start"`, which is good, but the buttons have no gap between icon and label in some cases.

**Recommendation:** Add a page title/header block, switch stat-card money icons to neutral/finance icons, and use the same button size (`size="sm"`) for the "View All" action as other secondary actions.

### `pages/groups/index.vue`

- Search input `class="w-64"` is too narrow on mobile.
- The green `+` create button uses `color="success" variant="outline" square` while most primary actions default to teal; it is also slightly larger than the refresh button due to different defaults.
- Group cards mix `text-primary` headings with `UBadge` status colors. The status badge uses a hardcoded euro prefix `€${...}` rather than `formatCurrency`.
- Delete/edit icon-only buttons are `size="sm"` and packed with `gap-1`, which is acceptable but could be `size="md"` for thumb friendliness.
- Empty state is good but the action button is teal, consistent with primary.

**Recommendation:** Make search `w-full sm:w-64`, use the standard primary color for the create button, use `formatCurrency` for the badge, and widen action touch targets.

### `pages/groups/[id]/index.vue` (Group detail)

- `GroupsSectionHeader` is inside `UCard variant="soft"` header; the card has no footer, so the tabs and content sit flush.
- There is no loading state: if `fetchGroup` fails or is slow, the user sees an empty header.
- The `GroupsTabsNav` uses `UTabs` with URL-based tab switching — good for deep linking.

**Recommendation:** Add a loading overlay or `UiLoadingSpinner` when `currentGroup` is not loaded, and a retry state on error.

### `pages/groups/[id]/members.vue`

- Pending invitations use tiny `size="xs"` icon-only buttons (resend/revoke) with only `gap-2`. Touch targets are too small and too close together.
- The `Invite User` button in the footer is right-aligned, which is fine on desktop but on mobile would benefit from `w-full sm:w-auto`.
- Member count badge is subtle and clear.

**Recommendation:** Change pending-invitation action buttons to `size="sm" square` with `gap-3`, and make the footer CTA full-width on mobile.

### `pages/groups/[id]/invite.vue`

- Good use of `UiCardHeader` with back link.
- Shows `UiLoadingSpinner` while group data loads — consistent.
- The invite form itself (`components/groups/InviteUsersForm.vue`) uses a narrow email input + button layout. The button label is "Invite" while the page title is "Invite User".

**Recommendation:** Consider whether the form should be inside a `UCard` body or bare; currently it is bare within the page card, which looks fine but differs from other forms.

### `pages/groups/[id]/imports/index.vue`

- Page uses `variant="soft"` on the outer card while other detail pages do not — inconsistent.
- Status badges map custom ID → color in the page; this logic could live in a utility or composable so it is not duplicated.
- The failed-import `UAlert` has a hardcoded title "Import Failed"; using `color="error"` + an icon is good.
- Pagination appears only when `totalPages > 1`, which is correct.

**Recommendation:** Remove the outer `variant="soft"` or apply it consistently across all group detail pages; extract status color mapping to a shared helper.

### `pages/groups/[id]/imports/add.vue`

- Step indicator uses `UBadge` + chevron icons. The active step uses `variant="solid"` while inactive uses `variant="soft"` — clear enough, but on small screens three badges + chevrons may wrap awkwardly.
- `UFileUpload` is used, but the import type select is a regular `USelect`. The "Analyze File" button disables correctly.
- The `currentStep === 'analysis'` block shows `ImportAnalysisResults` wrapped in a `UCard`, and then the outer page card also wraps it, causing a double-card effect.

**Recommendation:** Make the step indicator horizontally scrollable or allow wrapping; remove the redundant outer card padding when `ImportAnalysisResults` already provides a card.

### `pages/groups/[id]/expenses/[expenseId]/edit.vue`

- Page is a thin wrapper around `ExpensesExpenseForm`. It uses `router.back()` on cancel while other edit pages use `navigateTo(...)`.
- No page-level loading state.

**Recommendation:** Use `navigateTo(`/groups/${groupId}`)` for consistency with other cancel actions, or at least make the behavior identical across add/edit.

### `pages/expenses/add.vue`

- Same wrapper pattern as edit; it pre-selects the group via query param.
- `clearReceiptImage()` is cleaned up on unmount — good UX detail.
- "Add More" dropdown uses a split button with `UFieldGroup` — good, but the dropdown chevron button is only `size="lg"` via the group; visually the two buttons are misaligned in color because one is `subtle` and the other `outline`.

**Recommendation:** Make the "Add More" split-button pair use the same variant family (e.g., both outline or both subtle) so they look like one control.

### `pages/settings/2fa/setup.vue`

- Page does not use the default `py-8` rhythm; instead it has `max-w-lg mx-auto space-y-6 p-4`.
- QR code container has `bg-white rounded-lg` which will be a bright rectangle in dark mode; it should use a neutral background that works in both modes, or add `dark:bg-white` to preserve QR contrast.
- Backup codes are in a `grid grid-cols-2` which is fine on mobile.
- The three cards (`status`, `enrolling`, `verifying`, `disabling`) use bare `<UCard>` without headers for some and headers for others; inconsistent card padding.

**Recommendation:** Standardize 2FA card headers and QR-code background; align page padding with the rest of the app (`py-6` or `py-8`).

### `pages/profile.vue`

- Uses `max-w-2xl` centered card, which is consistent with forms.
- 2FA status row uses a hand-rolled `border-muted/30 bg-muted/10` box; this is actually a nice pattern, but it is unique to this page.
- The `User ID` copyable input uses `UFormField` but the disabled state styling is faint; the copy button is small but acceptable.
- Error state is bare text + retry button. The button does not use a full-width style.

**Recommendation:** Extract the 2FA status row as a small `UiStatusRow` primitive and reuse it in the 2FA setup page; make the retry button full-width on mobile.

### `pages/admin/users/index.vue`

- Reuses dashboard stat skeletons and stat cards, which is good.
- Search bar has the same `w-64` issue as groups.
- Pending-invitation cards use `variant="outline"` while user cards also use `variant="outline"` — consistent.
- The user grid title "Users" is `text-2xl font-bold text-primary`, matching groups list.
- `appVersionStats` is shown as a stat card value; that works but may feel odd in a stats row.

**Recommendation:** Fix search input width on mobile; consider moving app version to a footer or about area rather than a stat card.

### `pages/admin/users/[id]/edit.vue`

- Thin wrapper around `AdminUserForm`, consistent with group edit/add.
- No loading/error state shown while fetching the user.

**Recommendation:** Add a `UiLoadingSpinner` and error/retry behavior.

---

## Mobile-Specific Findings

1. **Viewport height units are outdated.** Login and verify use `h-screen`; on iOS Safari this can cause the card to be cut off or scroll oddly when the on-screen keyboard appears. Use `min-h-dvh` and `svh` fallbacks where needed.
2. **Search inputs are not full-width.** `pages/groups/index.vue` and `pages/admin/users/index.vue` leave unused space on narrow screens.
3. **Tiny action buttons.** Multiple pages use `size="xs"` icon-only buttons (`members.vue`, `imports/index.vue`, `admin/users/index.vue`, `ExpenseCard.vue`). These are hard to tap accurately and are often right next to another tiny button.
4. **No sticky/fixed mobile affordances.** The app relies on the top `UHeader` drawer for navigation. There is no bottom tab bar or floating primary action button. For a PWA-like experience, a sticky FAB for "Add Expense" on the group detail page would help frequent users.
5. **Safe-area insets are not handled.** The PWA update button sits at `fixed bottom-6 right-6 z-50`; on iOS with a home indicator this is fine, but page bottom padding does not account for `env(safe-area-inset-bottom)`. Add `pb-safe` or `env(safe-area-inset-bottom)` padding to the main container/footer.
6. **Filter card is hidden by default on mobile.** `GroupsExpensesTab` hides the filter card on mobile behind a "Filters" button. This is a good pattern, but the toggle button label changes from "Filters" to "Filters (N)" and may shift layout when the number appears.
7. **Tables/grids on small screens.** `ImportMappingForm` uses `grid-cols-1 md:grid-cols-2` for mapping rows, which stacks nicely on mobile. However, the per-row border is hard-coded gray and does not use theme tokens.
8. **Receipt preview zoom behavior.** The receipt modal uses `max-h-[70vh]` and a zoom toggle. On small screens, the close/zoom buttons at the bottom can be cramped; consider making the image swipe-friendly.
9. **Toast placement.** `useToast` defaults may appear at the bottom-right on desktop. On mobile, bottom-center or top-center is safer to avoid covering bottom navigation or input fields.
10. **Button label truncation.** "Add Expense" button on `ExpensesTab` hides the label on small screens (`hidden sm:inline`), leaving only a `+` icon. This is acceptable but could be ambiguous; a more explicit label or tooltip would help.

---

## Prioritized Recommendations

1. **[High] [Low effort] Standardize auth page height and width.** Replace `h-screen` with `min-h-dvh` in `pages/index.vue` and `pages/auth/verify.vue`; make all auth cards `w-full max-w-md` consistently. Affected: `pages/index.vue`, `pages/auth/verify.vue`, `pages/forgot-password.vue`, `pages/reset-password.vue`, `pages/invite/accept.vue`.
2. **[High] [Low effort] Make search inputs full-width on mobile.** Change `class="w-64"` to `class="w-full sm:w-64 md:w-80"`. Affected: `pages/groups/index.vue`, `pages/admin/users/index.vue`.
3. **[High] [Medium effort] Enlarge mobile touch targets.** Convert all `size="xs"` action buttons to `size="sm" square` and ensure at least `gap-2` (prefer `gap-3`) between destructive actions. Affected: `pages/groups/[id]/members.vue`, `pages/groups/[id]/imports/index.vue`, `pages/admin/users/index.vue`, `components/groups/ExpenseCard.vue`, `components/admin/UserCard.vue`.
4. **[Medium] [Low effort] Unify semantic money colors.** Replace raw `text-green-600`/`text-red-600` with Nuxt UI `color="success"`/`color="error"` or theme tokens in `UserBalanceCard`, `MemberBalanceCard`, `ExpenseCard`, and `DashboardStatCard`.
5. **[Medium] [Low effort] Standardize page title/header styles.** Use `UiCardHeader` (or a new `UiPageHeader`) in every page that needs a title; align `text-2xl` vs `text-xl` usage. Affected: `pages/dashboard.vue`, `pages/groups/index.vue`, `pages/admin/users/index.vue`, `components/ui/CardHeader.vue`.
6. **[Medium] [Medium effort] Add page-level loading/error states.** Add `UiLoadingSpinner` and an error/retry `UiEmptyState` to group detail, invite, members, and admin user edit pages. Affected: `pages/groups/[id]/index.vue`, `pages/groups/[id]/members.vue`, `pages/groups/[id]/invite.vue`, `pages/admin/users/[id]/edit.vue`.
7. **[Medium] [Low effort] Fix hard-coded gray borders and raw text colors in import components.** Replace `border-gray-200 dark:border-gray-700` with `border-muted` in `ImportMappingForm` and `ImportAnalysisResults`; replace raw `text-primary` use where redundant.
8. **[Medium] [Low effort] Standardize toast duration and position.** Add `duration: 4000` and a mobile-friendly `position` to all notifications in `useNotifications.js`.
9. **[Low] [Low effort] Switch remaining Heroicons to Lucide.** Change `i-heroicons-*` icons to `i-lucide-*` equivalents in `ActionsDropdown`, `ExpenseForm` split menu, and `ButtonDropdown` defaults.
10. **[Low] [Medium effort] Add safe-area padding and consider a mobile FAB.** Add `pb-[env(safe-area-inset-bottom)]` to `default.vue` layout; consider a sticky "Add Expense" FAB on the group expenses tab for one-tap creation.

---

## Out of Scope

- No source files were modified during this review.
- No new design system, complete redesign, or animated interactions were proposed.
- Backend behavior, API contracts, and business logic were not evaluated.
- Dead code was noted but not removed; examples include the unused `GenericModal.vue` export path through `useModal` (it is actually used) and the `DatePicker.vue` primitive (appears unused in favor of `InputDate.vue`).

---

## Verification

Report written to `/home/j1mm0/Workspace/splitduo/UI-DESIGN-REVIEW.md`.  
Sections present: Summary, Strengths, Cross-cutting Issues, Per-page Findings, Mobile-specific Findings, Prioritized Recommendations, Out of Scope.
