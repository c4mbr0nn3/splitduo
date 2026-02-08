# Invitation System

## Overview

Replace the current "add member to group" workflow — which requires users to already exist — with an email-based invitation system. Group admins type an email address; the system either adds the user immediately (if registered) or sends an invitation email with a registration link.

Additionally, remove the admin user creation endpoint (`POST /api/v1/users`). All user onboarding happens through the invitation system. The system admin user list page remains but displays users separated by status: active (registered) and pending (invited but not yet registered).

## User Flows

### Flow 1: Inviting an Existing User

```
Admin types email → Backend finds existing user → Creates GroupMember → Sends notification email
```

1. Admin opens group members page, types an email address.
2. `POST /api/v1/groups/{groupId}/invitations` with `{ email }`.
3. Backend finds a user with that email.
4. Backend creates a `GroupMember` record (role: Member).
5. Backend sends notification email: "You've been added to {groupName}".
6. Frontend shows success toast.

### Flow 2: Inviting a New User

```
Admin types email → No user found → Create InvitationToken → Send invitation email
→ Recipient clicks link → Registration page → Submit → User created + added to all pending groups
```

1. Admin opens group members page, types an email address.
2. `POST /api/v1/groups/{groupId}/invitations` with `{ email }`.
3. Backend finds no user with that email.
4. Backend creates an `InvitationToken` record (48h expiry).
5. Backend sends invitation email with link: `{baseUrl}/invite/accept?token=xxx`.
6. Frontend shows success toast, member list shows email as "Pending".
7. Recipient clicks link → frontend `/invite/accept` page.
8. Frontend calls `GET /api/v1/invitations/validate?token=xxx` to validate token and get the email.
9. Registration form shows: email (read-only), first name, last name, password, confirm password.
10. User submits → `POST /api/v1/invitations/accept` with token, firstName, lastName, password.
11. Backend creates user (BaseUser, verified), resolves **all** pending invitations for that email across all groups, adds user as Member to each group.
12. User can log in immediately.

### Flow 3: Resend Invitation

1. Admin sees a pending invitation in the members list.
2. Admin clicks resend.
3. Backend revokes the old token, creates a new one (fresh 48h expiry), sends a new email.

### Flow 4: Revoke Invitation

1. Admin sees a pending invitation in the members list.
2. Admin removes the pending member (same UX as removing a regular member).
3. Backend marks the `InvitationToken` as revoked. The link in the original email becomes invalid.

## API Endpoints

### Send Invitation

```
POST /api/v1/groups/{groupId}/invitations
Authorization: Bearer {token} (group admin)
```

**Request:**
```json
{
  "email": "jane@example.com"
}
```

**Behavior:**
- If email belongs to an existing user already in the group → `409 Conflict`.
- If email belongs to an existing user not in the group → add as Member, send notification email, return `200` with member data.
- If email has a pending invitation for this group → `409 Conflict` (duplicate invitation).
- If email is unknown → create `InvitationToken`, send invitation email, return `201` with pending invitation data.

**Response (existing user added):**
```json
{
  "success": true,
  "data": {
    "type": "member_added",
    "member": { "groupId": "...", "userId": "...", "user": { ... }, "role": "member", "joinedAt": "..." }
  }
}
```

**Response (invitation sent):**
```json
{
  "success": true,
  "data": {
    "type": "invitation_sent",
    "invitation": { "id": "...", "email": "jane@example.com", "invitedAt": "...", "expiresAt": "..." }
  }
}
```

### List Pending Invitations

```
GET /api/v1/groups/{groupId}/invitations
Authorization: Bearer {token} (group admin)
```

Returns all non-revoked, non-accepted, non-expired invitations for the group.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": "...",
      "email": "jane@example.com",
      "invitedBy": { "id": "...", "firstName": "...", "lastName": "..." },
      "invitedAt": "...",
      "expiresAt": "..."
    }
  ]
}
```

### Resend Invitation

```
POST /api/v1/groups/{groupId}/invitations/{invitationId}/resend
Authorization: Bearer {token} (group admin)
```

Revokes old token, creates new token with fresh 48h expiry, sends new email. Returns `200` with updated invitation data.

### Revoke Invitation

```
DELETE /api/v1/groups/{groupId}/invitations/{invitationId}
Authorization: Bearer {token} (group admin)
```

Sets `RevokedAt` on the token. Returns `204`.

### Validate Invitation Token (Public)

```
GET /api/v1/invitations/validate?token=xxx
No authentication required.
```

**Response (valid):**
```json
{
  "success": true,
  "data": {
    "email": "jane@example.com",
    "groupName": "Household",
    "expiresAt": "..."
  }
}
```

**Response (invalid/expired/revoked):**
```json
{
  "success": false,
  "error": { "code": "INVALID_TOKEN", "message": "This invitation link is invalid or has expired." }
}
```

### Accept Invitation (Public)

```
POST /api/v1/invitations/accept
No authentication required.
```

**Request:**
```json
{
  "token": "raw-token-string",
  "firstName": "Jane",
  "lastName": "Doe",
  "password": "SecurePass1!",
  "confirmPassword": "SecurePass1!"
}
```

**Behavior:**
1. Validate token (not expired, not revoked, not already accepted).
2. Validate password complexity (existing rules: min 8 chars, uppercase, lowercase, digit, special char).
3. Validate `password === confirmPassword`.
4. Create `User` (email from token, GlobalRole = BaseUser).
5. Find **all** valid pending invitation tokens for this email (across all groups).
6. Create `GroupMember` records for each group.
7. Mark all resolved tokens as accepted (`AcceptedAt = now`).
8. Return success. User can now log in.

**Response:**
```json
{
  "success": true,
  "data": {
    "message": "Account created successfully. You can now log in."
  }
}
```

## Data Model

### InvitationToken (new table: `invitation_tokens`)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `int` | PK, auto-increment | Internal |
| `Guid` | `uuid` | Unique, not null | API-facing ID |
| `Email` | `varchar(255)` | Not null | Invitee email (lowercased) |
| `GroupId` | `int` | FK → `groups.Id`, not null | Target group |
| `InvitedByUserId` | `int` | FK → `users.Id`, not null | Admin who sent invite |
| `TokenHash` | `varchar(255)` | Unique, not null | SHA256 hash of raw token |
| `ExpiresAt` | `timestamp` | Not null | Created + 48 hours |
| `AcceptedAt` | `timestamp` | Nullable | Set when user registers |
| `RevokedAt` | `timestamp` | Nullable | Set when admin revokes |
| `CreatedAt` | `timestamp` | Not null, default now | |

**Indexes:**
- `TokenHash` (unique) — token lookup on accept/validate.
- `Email, GroupId` (filtered: `AcceptedAt IS NULL AND RevokedAt IS NULL`) — duplicate detection.
- `Email` (filtered: `AcceptedAt IS NULL AND RevokedAt IS NULL`) — batch resolve on registration.
- `GroupId` (filtered: `AcceptedAt IS NULL AND RevokedAt IS NULL`) — list pending per group.

### No changes to existing tables

`User` and `GroupMember` entities remain unchanged. No new status fields needed — users created via invitation are immediately verified (no `IsVerified` flag needed since the token-based flow itself proves email ownership).

## Backend Changes

### Remove: Admin User Creation

- Delete `POST /api/v1/users` endpoint (currently in `UsersController.CreateUser`).
- Delete `CreateUserRequestDto` and `CreateUserResponseDto`.
- Delete `UsersService.CreateUserAsync()`.
- The admin welcome email template in this flow is also removed.
- `GET /api/v1/users` (list users) remains unchanged.

### New: System-Wide Pending Invitations for Admin Panel

```
GET /api/v1/users/pending
Authorization: Bearer {token} (SystemAdmin)
```

Returns all unique emails with at least one non-revoked, non-accepted, non-expired invitation across the system, with the groups they've been invited to.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "email": "jane@example.com",
      "groups": [
        { "id": "...", "name": "Household", "invitedAt": "...", "expiresAt": "..." }
      ]
    }
  ]
}
```

## Frontend

### Modified: System Admin User List Page

- Split view into two sections/tabs: **Active Users** and **Pending Invitations**.
- **Active Users**: Current user list (registered users). No "Create User" button.
- **Pending Invitations**: List of emails with pending invitations, showing which groups they've been invited to. Read-only — management (resend/revoke) happens from the group members page.

### Modified: Group Members Page

- Replace `UCommandPalette` user search with a simple **email text input** + "Invite" button.
- Members list shows two sections or mixed list:
  - **Active members**: As today (name, email, role, remove button).
  - **Pending invitations** (admin-only visibility): Email, "Pending" badge, invited date, resend button, remove/revoke button.
- Removing a pending invitation calls `DELETE /invitations/{id}`.
- Resend button calls `POST /invitations/{id}/resend`.

### New: `/invite/accept` Page

- Public page (no auth required, no app layout/nav).
- On mount: extract `token` from query string, call `GET /invitations/validate?token=xxx`.
- If invalid/expired: show error message with no form.
- If valid: show registration form:
  - Email (read-only, pre-filled from validate response).
  - First Name (required, max 100).
  - Last Name (required, max 100).
  - Password (required, show rules inline).
  - Confirm Password (required, must match).
- Submit calls `POST /invitations/accept`.
- On success: redirect to `/login` with a success message.

### Password Rules Display

Show password requirements inline on the form (checklist that updates as user types):
- At least 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter
- At least 1 number
- At least 1 special character (`!@#$%^&*()_+-=[]{}|;:,.<>?`)

## Email Templates

### Invitation Email (new user)

**Subject:** You've been invited to join {groupName} on SplitDuo

**Body:**
```
Hello,

{inviterName} has invited you to join the group "{groupName}" on SplitDuo.

To get started, create your account by clicking the link below:

[Create Account]({baseUrl}/invite/accept?token={rawToken})

This link will expire in 48 hours.

If you did not expect this invitation, you can safely ignore this email.
```

### Added to Group Email (existing user)

**Subject:** You've been added to {groupName} on SplitDuo

**Body:**
```
Hello {firstName},

{inviterName} has added you to the group "{groupName}" on SplitDuo.

You can view the group here:

[View Group]({baseUrl}/groups/{groupGuid})

```

## Edge Cases

| Scenario | Behavior |
|---|---|
| Admin invites same email to same group twice (pending) | `409 Conflict` — "An invitation for this email is already pending" |
| Admin invites existing group member | `409 Conflict` — "User is already a member of this group" |
| Token used after expiry | Validate/accept returns error: "This invitation link has expired" |
| Token used after revocation | Validate/accept returns error: "This invitation link is no longer valid" |
| User invited to 3 groups, clicks one link | Registration creates user + adds to all 3 groups (resolves all pending tokens for that email) |
| Invited email has mixed case | Lowercase email before storing and comparing |

## Configuration

Uses existing `AppOptions.BaseUrl` (`SD_BASE_URL` env var, defaults to `http://localhost:3000`) for email links. No new configuration needed.

## Out of Scope

- Self-registration (will not be implemented).
- Admin user creation (removed — replaced by invitation system).
- Invitation acceptance while logged in.
- Bulk invitations (CSV import of emails).
- Invitation to specific role other than Member.
