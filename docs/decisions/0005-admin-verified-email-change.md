# ADR-0005: Admin-initiated, verified email changes

## Status
Accepted (2026-10-31).

## Context
Issue #52 found that `PUT /api/v1/users/me` let a logged-in user change
their account email with no proof of ownership, no re-authentication and no
session invalidation. A compromised session could point the account at a
mailbox the attacker controls, trigger a password reset there, and take the
account over permanently. The conflict response also revealed whether an
address was registered.

The app is self-hosted and usually has a single SystemAdmin. A purely
self-service fix would need password + 2FA re-authentication, a profile UI
change and a second identity flow. The product decision was to remove
self-service email editing entirely and give the existing admin user page a
verified change action.

## Decision
- Email is read-only in the profile. `PUT /users/me` and `PUT /users/{id}`
  return `400` when `email` is present; the field stays on
  `UpdateUserRequestDto` so the rejection is explicit rather than a silently
  ignored JSON property.
- Only a SystemAdmin may request a change, and only after re-entering their
  own current password. Any admin may change any user's email, including
  their own, so single-admin instances remain recoverable.
- The change is confirm-first. `POST /users/{id}/email-change` stores the
  pending address on the user row and emails a 1-hour, single-use link to the
  new address. `POST /auth/confirm-email-change` applies it.
- The confirmation endpoint is public and token-only. The submitted token is
  the sole authority; missing, unknown, expired, used, soft-deleted-target and
  address-taken failures all return one generic error.
- Applying a change sets `Email` to the pending address, clears the pending
  fields, rotates `SecurityStamp`, revokes all refresh tokens, marks unused
  `password_reset` tokens used, and emails the old address.
- A second request silently overwrites the previous pending change; there is
  no explicit cancel endpoint.
- Email addresses are trimmed and lowercased by `EmailNormalizer` on every
  write and every lookup. The unique index is a raw-SQL expression index on
  `lower(email) WHERE deleted_at IS NULL`, so soft-deleted addresses are
  reusable and case variants collide.
- Notifications use the target user's `UiLanguage`. Two templates were added:
  `EmailChangeVerification` (new address) and `EmailChanged` (old address).
- Every request and apply is logged through Serilog with the acting admin,
  target user, old/new address and outcome.

## Rationale
- **Admin-only removes the self-service attack surface.** The original threat
  was a stolen session changing its own account email. With the field gone
  from the profile, that path does not exist.
- **The admin session is not enough on its own.** A stolen admin session could
  otherwise redirect a victim's address to an attacker mailbox and verify it.
  Re-entering the admin password is a cheap control that a session token theft
  does not satisfy.
- **Confirm-first proves ownership.** It matches ASP.NET Core Identity,
  Okta and Firebase `verifyBeforeUpdateEmail`; an immediate apply would let a
  typo or an attacker-controlled address become the login identity before
  anyone proves control of it.
- **Token-only confirmation reuses an accepted pattern.** It mirrors the
  password-reset ADR-0004: the token identifies the user, the email is never
  used for lookup or authorization, and every failure is indistinguishable.
- **Old-address notification is the recovery signal.** The old mailbox learns
  the new address and can contact the operator. It is sent on apply because
  the request itself is not a change; the admin's password and the
  new-address confirmation already gate the transition.
- **Session invalidation is mandatory.** The email is the login identity, so
  outstanding refresh tokens must not survive the switch. Access tokens live
  at most 15 minutes and authorization never reads the email claim.
- **Lowercase normalization is the only durable uniqueness rule.** A plain
  unique index on `Email` would allow `Alice@x` and `alice@x` to coexist. The
  expression index also excludes soft-deleted rows, satisfying the
  address-reuse requirement.

## Consequences
- There is no self-service email change. A user who needs a new address asks
  a SystemAdmin. The profile email stays read-only.
- An admin can change any address, including another admin's. Re-auth,
  new-address verification and the old-address notice are the controls;
  there is no dual-control approval step.
- The old address is released immediately after apply and may be claimed by
  another account. There is no alias or quarantine period.
- The pending change is stored on the `users` row, so there can be only one
  pending change per user. A new request overwrites it.
- The expression index is not visible to EF Core's model. It is created and
  dropped by raw SQL in `AddVerifiedEmailChange`; future migrations will not
  manage it. Do not replace it with a plain index on `Email`.
- The confirmation endpoint's failure path writes when it clears a pending
  change whose address was taken, so the controller saves on failure as well
  as success for that endpoint.
- A concurrent double-confirm of the same token is still a check-then-set
  race, as with password reset. The first success rotates the stamp and
  revokes tokens; the second resolves no active token and fails generically.
- Two different users can both request the same pending address and both pass
  the apply-time check. The first save wins; the loser hits the unique index
  and returns a 500. The winning change is unaffected, and a retry returns the
  generic error once the pending row is cleared.
- In-flight 2FA email codes sent to the old address remain valid until their
  10-minute TTL. Using one still requires the account password, and the change
  already revokes all refresh tokens.
- Serilog audit entries follow the existing `LogCleanupJob` retention; there
  is no durable audit table.
- Access tokens issued before the change carry the old email claim until they
  expire. Nothing authorizes on that claim.

## Alternatives rejected
- **Self-service with password + 2FA re-auth.** The issue's original plan.
  It is a valid design, but it needs a second UI flow and does not fit the
  single-admin, self-hosted operating model chosen here.
- **Admin edits email immediately and notifies afterwards.** Simpler, but the
  new address becomes the login identity before ownership is proven.
- **Reserve the pending address with a second unique index.** It closes the
  request-vs-confirm race but adds schema and blocks a legitimate second
  request. The apply-time re-check plus generic failure is enough.
- **A separate `email_change_requests` table.** Cleaner for history, but the
  user row already has one pending slot and the flow has no multi-step state.
- **A global `EmailConfirmed` flag.** The app creates accounts through
  invitation acceptance and has never verified addresses at registration.
  Adding a flag would ripple through login and invitations for no gain; the
  change token proves control of the new address on its own.
- **Notify the old address at request time as well as on apply.** Stricter,
  but it adds a template and warns about changes that may never be confirmed.
  The admin re-auth and new-address verification already carry the risk.

## Sources
- OWASP ASVS 4.0.3 V2.2.3 (notify on credential/email changes) and the
  Authentication, Email Validation and Forgot Password Cheat Sheets.
- NIST SP 800-63B-4 (notify the subscriber after account events; reauthenticate
  or terminate sessions on sensitive changes).
- ASP.NET Core Identity `SetEmailAsync`, `GenerateChangeEmailTokenAsync` and
  `ChangeEmailAsync` (change email only after the token is confirmed; rotate
  the security stamp).
- Okta MyAccount email challenge and Firebase `verifyBeforeUpdateEmail`
  (confirm the new address before it becomes the login identity).
- Auth0 admin Management API (applies immediately and marks the address
  unverified) — the exception this ADR deliberately does not follow.
