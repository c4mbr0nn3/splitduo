# ADR-0002: Two-dimensional auth rate limiting (IP + account)

## Status
Accepted (2026-10-03)

## Context
Security review finding SEC-02 (High): the `"auth"` rate-limit policy was a
single **global** fixed-window bucket (no partitioner) — 11 anonymous
`POST /api/v1/auth/login` requests from *anyone* locked login + 2FA for the
whole deployment, and `refresh` / `forgot-password` / `validate-reset-token` /
`reset-password` had no limiter at all. Anonymous brute-force against login was
essentially free, and one noisy client could deny service to everyone.

## Decision
Auth endpoints are rate limited along two independent dimensions, both
sliding-window:

| Dimension | Bucket | Limits | Applies to |
|---|---|---|---|
| Per-IP (named policies) | `"ip:" + normalized client IP` | 10 / 1 min (`auth`); 30 / 1 min (`auth-refresh`) | `[EnableRateLimiting]` on auth actions |
| Per-account (GlobalLimiter) | `"acct:" + normalized email` | 15 / 15 min | any `/api/v1/*` request that carried an email (extracted by `UseAuthEmailExtraction`) |

Keying details: IPv4-mapped IPv6 is mapped back to IPv4; plain IPv6 keys on its
/64 network prefix; emails are trimmed + lowercased and, over 256 chars, hashed
(SHA256) so keys are deterministic and bounded. The 429 contract is preserved:
status 429, `Retry-After: 60`, empty body, no diagnostic detail (RFC 6585 §4
defines 429 + `Retry-After`; field semantics per RFC 9110 §10.2.3 — *not*
RFC 9116).

Pipeline order matters: `UseAuthEmailExtraction` sits between
`UseAuthentication` and `UseRateLimiter`; `UseRateLimiter` moved after
`UseAuthentication` (source-verified behavior, undocumented contract) so the
global limiter sees the extracted account key. Behind a reverse proxy,
per-IP limiting requires `SD_KNOWN_PROXIES` to be set so `X-Forwarded-For`
is honored — unset, forwarded headers are disabled (**fail-closed**, stricter
than the framework default of trusting loopback) and per-IP limiting collapses
to one bucket.

## Rationale
- **Why `GlobalLimiter` composition:** the public
  `System.Threading.RateLimiting` API exposes no composite limiter;
  `PartitionedRateLimiter.CreateChained` is only assignable to
  `RateLimiterOptions.GlobalLimiter`, and we need per-policy + global
  dimensions simultaneously. The per-account dimension therefore lives on
  `GlobalLimiter` (`GetNoLimiter` for requests without an email, so only the
  per-IP dimension applies to non-auth, static, and health requests).
- **Sliding window over fixed:** avoids the boundary-burst effect of
  fixed windows (2× quota in a boundary straddle), consistent with OWASP
  guidance on bot-management / brute-force throttling.
- **Limits:** 15 / 15 min per account = 60/hr, within NIST SP 800-63B §5.2.2's
  ceiling of 100 consecutive failed attempts per account (agencies may impose
  lower limits; the older “100 per hour” phrasing is superseded).
- The per-IP buckets use the client's real IP via forwarded headers when
  configured, so NAT'd office clients are not all one bucket unless they share
  the proxy's view.

## Consequences / guarantees
A client partition is never evicted while its budget is partially consumed;
eviction occurs only ≥10s after the window has fully replenished, and
therefore never resets or softens an in-flight window. Attackers cannot
recycle a fresh budget by pausing. (This assumes `AutoReplenishment = true` —
the default we use on every limiter here. If `AutoReplenishment = false` is
ever introduced, the idle-eviction heartbeat becomes the replenisher and this
guarantee changes.)

## Residual risks
- **Uncapped partition cardinality:** in-memory store; idle partitions are
  evicted after ~10s and there is no hard cap. Any multi-instance topology
  (per-process dictionaries) is the trigger to move to a distributed store /
  Redis-backed limiter.
- **NAT / shared-IP:** many humans behind one egress IP share a per-IP bucket;
  the per-account dimension is the compensating control.
- **IPv6 /64 normalization:** all clients inside one /64 share a bucket —
  intentional trade-off against per-address partition explosion.
- **SEC-13 sequencing:** the 2FA per-user attempt cap is unchanged and runs
  after these limits; not redesigned here.
- **Normalization asymmetry (known, accepted):** the limiter lowercases emails
  for bucketing, but the login lookup (`AuthenticationService.cs:41-42`) is
  case-sensitive — two case variants of one address can be separate *accounts*
  while sharing a rate-limit *bucket*. Safe direction (over-constrains, never
  under-constrains), but worth remembering.

Satisfies the OWASP ASVS 5.0 §6.1.1 documentation requirement ("verify the
application enforces login rate limiting and documents the algorithm").

## Revisit triggers
Multi-instance deployment (→ distributed limiter), a change to
`AutoReplenishment`, or any feature needing a third limiting dimension.