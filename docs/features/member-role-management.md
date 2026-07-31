# Member Role Management

## Overview

Allow a group admin to promote a member to group admin and demote a group admin back to a member. The `GroupRole` enum (`Admin`, `Member`) and the `role_id` column already exist — this feature adds the endpoint, service logic, and frontend UI to change an existing member's role.

## User Flows

### Flow 1: Promote a Member to Admin

```
Admin opens members page → clicks "Promote to Admin" on a member row → confirmation modal → role updated → badge changes to admin
```

1. Admin opens `/groups/{id}/members`.
2. Each member row shows an actions dropdown (admin viewers only). For a `member`-role row, the dropdown includes "Promote to Admin".
3. Admin clicks "Promote to Admin" → confirmation modal: "Promote {name} to admin? They will be able to edit group settings, manage members, and delete the group."
4. Admin confirms → `PUT /api/v1/groups/{groupId}/members/{userId}/role` with `{ "role": "admin" }`.
5. Backend verifies caller is admin, target is a member, updates `role_id`.
6. Frontend refreshes the member list. The promoted member's badge changes to admin (crown icon, primary color).

### Flow 2: Demote an Admin to Member

```
Admin opens members page → clicks "Demote to Member" on an admin row → confirmation modal → role updated → badge changes to member
```

1. Admin opens `/groups/{id}/members`.
2. For an `admin`-role row (other than themselves), the dropdown includes "Demote to Member".
3. Admin clicks "Demote to Member" → confirmation modal: "Demote {name} to member? They will no longer be able to manage this group."
4. Admin confirms → `PUT /api/v1/groups/{groupId}/members/{userId}/role` with `{ "role": "member" }`.
5. Backend verifies caller is admin, target is an admin, **last-admin protection** check, updates `role_id`.
6. Frontend refreshes the member list. The demoted admin's badge changes to member (user icon, secondary color).

### Flow 3: Self-Demotion Blocked

An admin cannot demote themselves. The dropdown on the current user's own row does not show "Demote to Member". If the API is called directly for self-demotion, the backend returns `403 Forbidden`.

## API Endpoint

### Change Member Role

```
PUT /api/v1/groups/{groupId}/members/{userId}/role
Authorization: Bearer {token} (group admin)
```

**Request:**

```json
{
  "role": "admin"
}
```

- `role` must be `"admin"` or `"member"` (case-insensitive). Any other value → `400 Bad Request`.

**Response (success):**

```json
{
  "success": true,
  "data": {
    "groupId": "...",
    "userId": "...",
    "user": { "id": "...", "email": "...", "firstName": "...", "lastName": "..." },
    "role": "admin",
    "joinedAt": 1234567890
  }
}
```

Returns the updated `GroupMemberDto` (same shape as `GET /groups/{groupId}/members` items).

**Error responses:**

| Condition                              | Status | Code           | Message                                                    |
| -------------------------------------- | ------ | -------------- | ---------------------------------------------------------- |
| `role` is missing or invalid           | 400    | `BAD_REQUEST`  | "Invalid role. Must be 'admin' or 'member'."               |
| Caller is not a group member           | 403    | `FORBIDDEN`    | "Access to this group is not allowed"                      |
| Caller is a member but not admin       | 403    | `FORBIDDEN`    | "Only group administrators can change member roles"       |
| Target user is not a member of group   | 404    | `NOT_FOUND`    | "User is not a member of this group"                       |
| Caller tries to change their own role  | 403    | `FORBIDDEN`    | "You cannot change your own role"                          |
| Demoting the last admin                | 409    | `CONFLICT`     | "Cannot demote the only administrator of the group"        |
| New role equals current role           | 400    | `BAD_REQUEST`  | "User already has this role"                               |

## Backend Changes

### New DTO: `UpdateGroupMemberRoleRequestDto`

**File:** `sd-backend/SplitDuo.Api/Features/Groups/Dto/UpdateGroupMemberRoleRequestDto.cs`

```csharp
public class UpdateGroupMemberRoleRequestDto
{
    [Required] public string Role { get; set; } = "";
}
```

### New Service Method: `ChangeMemberRoleAsync`

Add to `IGroupsService` interface and `GroupsService` implementation.

```csharp
Task<Result<GroupMemberDto>> ChangeMemberRoleAsync(string groupId, string userId, Guid currentUserId, UpdateGroupMemberRoleRequestDto request);
```

**Logic:**

1. Parse `groupId` (Guid) and `userId` (Guid) — invalid → `400`.
2. Parse `request.Role` via `Enum.TryParse<GroupRole>(request.Role, true, out var newRole)` — invalid → `400`.
3. Find group by Guid (non-deleted) — not found → `404`.
4. Find caller's `GroupMember` (non-deleted) — not found → `403`.
5. Check caller `Role == Admin` — no → `403`.
6. Find target's `GroupMember` (non-deleted) — not found → `404`.
7. If `target.UserId == caller.UserId` → `403` (cannot change own role).
8. If `target.Role == newRole` → `400` (already has this role).
9. If demoting (`newRole == Member` and `target.Role == Admin`):
   - Count active admins in group. If `<= 1` → `409` (last admin protection).
10. Set `target.Role = newRole`, update `UpdatedAt`.
11. `SaveChangesAsync` (in controller, following existing pattern).
12. Return `GroupMemberDto` mapped from target.

### New Controller Endpoint

Add to `GroupsController`:

```csharp
[HttpPut("{groupId}/members/{userId}/role")]
[Authorize]
public async Task<IActionResult> ChangeMemberRole(string groupId, string userId, [FromBody] UpdateGroupMemberRoleRequestDto request)
{
    var currentUserId = userContextService.GetCurrentUserId();
    var result = await groupsService.ChangeMemberRoleAsync(groupId, userId, currentUserId, request);
    if (result.IsSuccess)
        await unitOfWork.SaveChangesAsync();
    return HandleResult(result, "Member role updated successfully");
}
```

Follows the exact pattern of `UpdateGroup` / `AddGroupMember` / `RemoveGroupMember`.

### No Migration Needed

The `role_id` column already exists on `group_members`. This feature only changes values in an existing column.

## Frontend Changes

### New API Method: `useGroups.js`

Add to the `useGroups` composable:

```javascript
changeMemberRole(groupId, userId, role) {
  return api.put(`/groups/${groupId}/members/${userId}/role`, { role })
}
```

### Modified: Members Page (`app/pages/groups/[id]/members.vue`)

- After a successful role change, re-fetch members (`fetchGroupMembers`) or update the local `members` array in place.
- The `isGroupAdmin` computed already exists (line 238-240) — reuse it to gate the actions dropdown.

### Modified: Member Row Component (`app/components/groups/members/Row.vue`)

Add an actions dropdown (visible only when the current user is a group admin and the row is not the current user themselves):

- **For `member`-role rows:** "Promote to Admin" item with `i-lucide-arrow-up-circle` icon.
- **For `admin`-role rows:** "Demote to Member" item with `i-lucide-arrow-down-circle` icon.

Use `UDropdownMenu` (Nuxt UI v4) with items array, following the pattern in `GroupsActionsDropdown.vue`. On click:

1. Open confirmation modal via `useModal().warning()`.
2. On confirm, call `changeMemberRole(groupId, userId, newRole)`.
3. On success, emit event or call parent refresh.

### Modified: Member Card Component (`app/components/groups/members/Card.vue`)

The `Card.vue` component (used in alias-mode groups) should also expose the same actions dropdown in its header area, gated by the same admin check.

### Confirmation Modal Copy

**Promote:**
- Title: "Promote to Admin"
- Content: "{firstName} will be able to edit group settings, manage members, and delete this group."
- Confirm text: "Promote"

**Demote:**
- Title: "Demote to Member"
- Content: "{firstName} will no longer be able to manage this group."
- Confirm text: "Demote"

## Edge Cases

| Scenario                                         | Behavior                                                              |
| ------------------------------------------------ | -------------------------------------------------------------------- |
| Admin promotes an already-admin member           | `400` — "User already has this role"                                  |
| Admin demotes an already-member                  | `400` — "User already has this role"                                  |
| Admin tries to demote themselves                 | `403` — "You cannot change your own role" (also hidden in UI)        |
| Admin demotes the only other admin               | Succeeds — group still has the caller as admin                        |
| Admin demotes the last admin (themselves)        | Blocked by self-demotion check (`403`) before last-admin check        |
| Two admins, admin A demotes admin B              | Succeeds — admin A remains                                           |
| Non-admin member calls the endpoint              | `403` — "Only group administrators can change member roles"          |
| Target user was removed (soft-deleted)           | `404` — "User is not a member of this group"                          |
| Role string is `"ADMIN"` or `"Admin"`            | Accepted — case-insensitive parse                                     |
| Role string is `"owner"` or `"superadmin"`       | `400` — "Invalid role. Must be 'admin' or 'member'."                 |

## Out of Scope

- **Transfer ownership** — there is no "owner" role; the group creator (`CreatedBy`) is just a reference. Not addressed here.
- **Role history / audit log** — no tracking of role changes beyond the existing `UpdatedAt` timestamp.
- **Notifications** — no email or in-app notification sent on role change. (Can be added later.)
- **Multiple admin levels** — only `Admin` and `Member` exist; no "moderator" or custom roles.
- **Self-promotion** — a member cannot promote themselves; only an existing admin can promote.