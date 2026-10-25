# ADR-0004: Password-reset tokens are the sole authority

## Status
Accepted (2026-10-25).

## Context
Security finding **SEC-14 / L-03**: `GET /api/v1/auth/validate-reset-token` and
`POST /api/v1/auth/reset-password` first looked up the submitted email and
returned `Invalid email or token` when it did not match a live user, then
`Invalid or expired reset token` when the user existed but the token did not
match. An unauthenticated caller could therefore enumerate registered accounts
at the per-IP auth rate limit (CWE-204 observable response discrepancy).

The validate endpoint also incremented `Attempts` only after a successful
match and rejected once `Attempts >= MaxAttempts`. A legitimate link that was
prefetched by an email scanner or reloaded three times was rejected on the
fourth call, while wrong tokens never consumed an attempt.

Password-reset tokens are 512-bit random values, stored as SHA-256 hashes,
single-use, and expire after one hour. Attempt counters add no guessing
resistance against that token space.

## Decision
- Both reset endpoints resolve the token by `TokenHash` alone, then load the
  owning user through the token. The submitted email is accepted for
  backward-compatible links and for the global rate limiter's per-account
  partition, but it is never compared, never used for lookup, and never used
  for authorization.
- Every token failure returns the same `400` body with the
  `InvalidOrExpiredResetToken` message: missing, malformed, unknown, expired,
  used, or owned by a soft-deleted user.
- `validate-reset-token` is read-only and idempotent. It does not consume the
  token, rotate a stamp, or write to the database.
- Password-reset rows no longer use `Attempts` / `MaxAttempts`. The columns
  remain on the shared `two_factor_tokens` table only to avoid a schema
  migration; no code reads or writes them after this change.
- The endpoint and DTO shapes are unchanged, so the generated OpenAPI document
  and frontend types do not move.

## Rationale
- **Why the token, not the email.** The token is the capability. It already
  identifies the user through the foreign key, so an email lookup adds a
  second, attacker-controlled identifier that can only produce an oracle.
- **Why one message for every failure.** A distinct "expired" message is a
  token-state oracle. Collapsing it is cheap and removes a future invitation to
  reintroduce account-existence branching. The message keeps the useful action
  ("request a new link") without confirming whether the account exists.
- **Why validation must be read-only.** Mail scanners and link previews issue
  GET requests before the user clicks. Consuming or counting attempts on GET
  breaks the legitimate link. Single-use remains enforced at reset submit.
- **Why email stays in the contract.** Existing reset emails and the bundled
  SPA send `{ email, token }`. Removing it would change the OpenAPI contract for
  no security gain, because the service ignores it.

## Consequences
- Message, status, and timing no longer branch on account existence for these
  endpoints. A valid token resets the account even if the submitted email
  differs; the token already identifies the owner.
- A prefetched or repeatedly opened link keeps working until the reset submit
  or the one-hour expiry.
- The existing per-IP `auth` policy and the global per-account limiter still
  apply because clients keep sending `email`.
- Single-use is enforced by `UsedAt` inside the lookup predicate. Concurrent
  double-submit of the same token remains a pre-existing check-then-set race:
  both requests can pass `UsedAt == null` before either saves. This is accepted
  because it needs simultaneous requests with the same 512-bit token and the
  first successful reset still rotates the security stamp and revokes refresh
  tokens.
- Out of scope: the login lockout message and unknown-user password-hash timing
  are the other halves of SEC-14; forgot-password initiation timing is
  unchanged (the response is already generic and email delivery is queued).

## Alternatives rejected
- **Keep the email lookup and only unify the messages.** Safe for the response
  text, but leaves an email-driven branch and a timing difference, and invites
  the oracle back during future edits.
- **Remove `email` from the API and reset links.** Cleaner contract, but it
  changes the generated OpenAPI document and frontend types for no security
  gain and breaks old links unnecessarily.
- **Keep the attempt counter on validate.** It is a token-state oracle and a
  functional bug: link prefetching burns legitimate tokens while wrong guesses
  are never counted.
- **Dummy hashing for unknown emails.** It addresses login enumeration where a
  password hash is the expensive step. Here the token-hash lookup is already
  uniform and the email no longer drives the branch.

## Sources
- OWASP ASVS 5.0 §6.3.8 (observable response discrepancy, Level 3) and the
  Forgot Password Cheat Sheet (uniform responses, single-use tokens).
- NIST SP 800-63B-4 §3.2.2 (throttling), §4.2.1.2 (recovery-code lifetime),
  §4.6 (notify the subscriber after recovery).
- CWE-204 (observable response discrepancy) and CWE-203 (observable
  discrepancy).
- ASP.NET Core Identity `resetPassword` (user-not-found collapses to
  `InvalidToken`); Django `PasswordResetConfirmView` (token-only validation with
  a uniform failure page); Firebase `verifyPasswordResetCode` (token-only).
