# Platform Admin Role Management

## Overview

Allow a system admin to promote a base user to system admin and demote a system admin back to base user. The `GlobalRole` enum (`BaseUser`, `SystemAdmin`) and the `global_role_id` column already exist. The backend `PUT /api/v1/users/{userId}` endpoint already accepts a `GlobalRole` field in `UpdateUserRequestDto` and the service already applies it with an `isSystemAdmin` check. This feature closes the remaining gaps: last-SystemAdmin protection, inline promote/demote UI in the user card dropdown, confirmation modals, and tests.

## Current State

- **Backend**: `PUT /api/v1/users/{userId}` with `{ "globalRole": 2 }` already changes a user's role. The service checks `isSystemAdmin` before applying the role change (returns 403 for non-admins). However: no last-SystemAdmin guard, no self-demotion guard, and the endpoint lacks `[Authorize(Policy = "SystemAdmin")]` (relies on service-level check only).
- **Frontend**: The edit form at `/admin/users/[id]/edit` already has a role selector (`USelect` with `UserRole.getSelectOptions()`). The `AdminUserCard` dropdown has Edit, Revoke Tokens, Delete — but no inline promote/demote action.
- **Tests**: No tests exist for role change behavior.

## User Flows

### Flow 1: Promote a User to System Admin (inline)

```
Admin opens /admin/users → clicks "Promote to Admin" on a user card → confirmation modal → role updated → badge changes to Admin
```

1. System admin opens `/admin/users`.
2. Each `AdminUserCard` shows an actions dropdown. For a `BaseUser` (globalRoleId == 1), the dropdown includes "Promote to Admin".
3. Admin clicks "Promote to Admin" → confirmation modal: "Promote {name} to system admin? They will have full access to manage all users and platform settings."
4. Admin confirms → `PUT /api/v1/users/{userId}` with `{ "globalRole": 2 }`.
5. Backend verifies caller is SystemAdmin, target is BaseUser, updates `global_role_id`.
6. Frontend refreshes the user list. The promoted user's badge changes to Admin (crown icon, primary color).

### Flow 2: Demote a System Admin to User (inline)

```
Admin opens /admin/users → clicks "Demote to User" on an admin card → confirmation modal → role updated → badge changes to User
```

1. System admin opens `/admin/users`.
2. For a `SystemAdmin` card (globalRoleId == 2) other than themselves, the dropdown includes "Demote to User".
3. Admin clicks "Demote to User" → confirmation modal: "Demote {name} to regular user? They will no longer have access to the admin panel or manage platform users."
4. Admin confirms → `PUT /api/v1/users/{userId}` with `{ "globalRole": 1 }`.
5. Backend verifies caller is SystemAdmin, target is SystemAdmin, **last-SystemAdmin protection** check, updates `global_role_id`.
6. Frontend refreshes the user list. The demoted admin's badge changes to User (user icon, secondary color).

### Flow 3: Self-Demotion Blocked

A system admin cannot demote themselves. The dropdown on the current user's own card does not show "Demote to User". If the API is called directly for self-demotion, the backend returns `403 Forbidden`.

### Flow 4: Promote/Demote via Edit Form (existing, bug fix)

The edit form at `/admin/users/[id]/edit` already has a role selector, but it is **currently broken** — it sends `globalRoleId` in the payload while the backend DTO field is `GlobalRole` (serializes as `globalRole`). The role change silently no-ops (HTTP 200 with unchanged user). This feature fixes the edit form to send the correct field name. The inline dropdown action is a shortcut for the common case; the edit form remains for combined name/email/role updates.

## Backend Changes

### 1. Add self-demotion guard to `UpdateUserAsync`

**File:** `sd-backend/SplitDuo.Api/Features/Users/Services/UsersService.cs` (lines 230-275)

In the role-change block (after the `isSystemAdmin` check, before setting `user.GlobalRole`), add:

```csharp
if (request.GlobalRole.Value == GlobalRole.BaseUser && currentUserId == userGuid)
    return Result<UserDto>.Forbidden("You cannot change your own role");
```

This prevents a system admin from demoting themselves via the API.

### 2. Add last-SystemAdmin guard to `UpdateUserAsync`

In the role-change block, after the self-demotion check, before setting `user.GlobalRole`:

```csharp
if (request.GlobalRole.Value == GlobalRole.BaseUser && user.GlobalRole == GlobalRole.SystemAdmin)
{
    var adminCount = await unitOfWork.Users
        .CountAsync(u => u.GlobalRoleId == (int)GlobalRole.SystemAdmin && u.DeletedAt == null);
    if (adminCount <= 1)
        return Result<UserDto>.Conflict("Cannot demote the only system administrator");
}
```

This prevents the last system admin from being demoted (which would leave the platform without any admin).

**Note on reachability**: Unlike the group-level last-admin guard (which was unreachable due to the self-demotion check running first), this guard IS reachable. A second system admin can attempt to demote the only other system admin. If there are 2 admins and admin A demotes admin B, `adminCount` is 2, so it succeeds. But if admin A demotes admin B and admin A is then the only one... that's fine. The guard only triggers when `adminCount <= 1`, meaning the target is the only admin. Since the caller must be a different admin (self-demotion blocked), this scenario means the caller is NOT an admin — but the `isSystemAdmin` check already blocks that. So this guard is also unreachable in practice (same reasoning as group-level). However, it's kept as defense-in-depth: if the self-demotion guard is ever removed or bypassed, this prevents orphaning the platform.

### 3. Add `[Authorize(Policy = "SystemAdmin")]` to the role-change path

The `PUT /api/v1/users/{userId}` endpoint currently has only `[Authorize]` (no policy). It allows any authenticated user to call it, then the service checks `isSystemAdmin` for role changes and self-access for profile updates. This is intentional — users can edit their own profile via this endpoint.

**No change needed to the controller attribute.** The endpoint must remain accessible to non-admins for self-edits (name, email). The service-level `isSystemAdmin` check for role changes is the correct pattern here. Adding the policy would break self-edits.

### 4. No new endpoint, no new DTO, no migration

The existing `PUT /api/v1/users/{userId}` endpoint and `UpdateUserRequestDto.GlobalRole` field already handle role changes. Only the two guards above are added to the service.

## Frontend Changes

### 1. Add `changeUserRole` helper to `useUsers.js`

**File:** `sd-frontend/app/composables/resources/useUsers.js`

Add a convenience method that calls the existing `updateUser` with only the role field. **The payload key must be `globalRole`** (matching the backend DTO field `UpdateUserRequestDto.GlobalRole`), NOT `globalRoleId`:

```javascript
changeUserRole(userId, globalRoleId) {
  return api.put(`/users/${userId}`, { globalRole: globalRoleId })
}
```

This wraps the existing `updateUser` but sends only the role, avoiding accidental name/email overwrites. The parameter name `globalRoleId` is fine (frontend convention from `UserDto.GlobalRoleId`); only the payload key must be `globalRole`.

### 2. Fix the existing edit form field name (pre-existing bug)

**File:** `sd-frontend/app/pages/admin/users/[id]/edit.vue`

The edit form currently sends `globalRoleId` in the payload (via `UserForm.vue`'s `v-model="form.globalRoleId"`), but the backend DTO field is `GlobalRole` (serializes as `globalRole`). The role change silently no-ops. Fix by mapping the field name in `onSubmit`:

```javascript
async function onSubmit(formData) {
  try {
    const { globalRoleId, ...rest } = formData
    const updatedUser = await updateUser(userId, { ...rest, globalRole: globalRoleId })
    if (updatedUser) {
      navigateTo('/admin/users')
    }
  }
  catch (err) {
    console.error('Error updating user:', err)
  }
}
```

The form field stays `globalRoleId` (it's the response shape from `UserDto`); only the request payload key is remapped to `globalRole`.

### 2. Add promote/demote to `AdminUserCard` dropdown

**File:** `sd-frontend/app/components/admin/UserCard.vue` (154 lines)

Add items to the existing actions dropdown (which currently has Edit, Revoke Tokens, Delete):

- **For BaseUser cards (globalRoleId == 1):** "Promote to Admin" item with `i-lucide-arrow-up-circle` icon, `color: 'success'`.
- **For SystemAdmin cards (globalRoleId == 2) that are NOT the current user:** "Demote to User" item with `i-lucide-arrow-down-circle` icon, `color: 'warning'`.
- **For the current user's own card:** No promote/demote items (self-demotion is blocked; self-promotion is unnecessary since they're already admin).

On click:
1. Open confirmation modal via `useModal().warning()`.
2. On confirm, call `changeUserRole(user.id, newRoleId)`.
3. Emit `@refresh` event so the parent page re-fetches users.

The dropdown needs `currentUserId` to determine if the card is the current user. Pass it as a prop from the parent page, or use `useAuth()` directly in the component. Check how `AdminUserCard` already receives props and how `useAuth` is used elsewhere.

### 3. Wire up refresh in `/admin/users/index.vue`

**File:** `sd-frontend/app/pages/admin/users/index.vue` (271 lines)

Listen for `@refresh` from `AdminUserCard` and call the existing user-fetch function. Follow the existing refresh pattern used for delete/revoke actions.

### 4. Confirmation Modal Copy

**Promote:**
- Title: "Promote to System Admin"
- Content: "{firstName} will have full access to manage all users and platform settings."
- Confirm text: "Promote"

**Demote:**
- Title: "Demote to Regular User"
- Content: "{firstName} will no longer have access to the admin panel or manage platform users."
- Confirm text: "Demote"

## Edge Cases

| Scenario                                         | Behavior                                                              |
| ------------------------------------------------ | -------------------------------------------------------------------- |
| Admin promotes an already-admin user             | `400` — "User already has this role" (consistent with group-level)    |
| Admin demotes an already-base-user               | `400` — "User already has this role" (consistent with group-level)    |
| Admin tries to demote themselves                 | `403` — "You cannot change your own role" (also hidden in UI)        |
| Non-admin tries to change a role                 | `403` — "Only system administrators can modify user roles"           |
| Demoting the last system admin                   | `409` — "Cannot demote the only system administrator" (defense-in-depth only; not reachable under current guards — self-demotion check fires first) |
| Two admins, admin A demotes admin B              | Succeeds — admin A remains                                           |
| Target user was soft-deleted                     | `404` — "User not found"                                              |
| Role value is invalid (not 1 or 2)              | `400` — ASP.NET model binding rejects invalid enum value              |

## Out of Scope

- **Transfer ownership** — there is no "platform owner" concept; the seeded admin is just a SystemAdmin.
- **Role history / audit log** — no tracking of role changes beyond the existing `UpdatedAt` timestamp.
- **Notifications** — no email or in-app notification sent on role change. (Can be added later.)
- **Multiple admin levels** — only `BaseUser` and `SystemAdmin` exist; no custom roles.
- **Self-promotion** — a BaseUser cannot promote themselves; only an existing SystemAdmin can promote.
- **Revoking tokens on demotion** — when a user is demoted, their existing JWT (with the admin role claim) remains valid until it expires (15 minutes). This is acceptable given the short token lifetime. For security-sensitive demotions, the admin should also click "Revoke Tokens" (which already exists in the dropdown).
- **Race condition (two admins demote each other simultaneously)** — Two SystemAdmins could demote each other at the same time, leaving the platform with no admins. This is a TOCTOU race that would require transaction-level locking to fully prevent. Accepted as out of scope given the tiny window, the requirement for two simultaneous actions, and the DB-level recovery path (direct SQL update of `global_role_id`).