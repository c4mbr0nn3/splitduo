# ADR-0003: Health endpoint hardening (cached readiness, status-only public body)

## Status
Accepted (2026-10-24). Partially supersedes ADR-0002 for `/health`.

## Context
Security finding **SEC-11 / L-02**: `MapHealthChecks("/health")` with
`AddNpgSql` and a custom response writer was unauthenticated and unthrottled,
opened a PostgreSQL connection on every request, and returned per-check
statuses plus exception descriptions. An anonymous loop could exhaust
`max_connections` and stall the EF pool; the body disclosed internal check
names and error text (CWE-400, CWE-200).

SplitDuo is a single container behind an optional Caddy reverse proxy. Probes
(container, uptime monitor, future orchestrator) cannot authenticate, and the
app must keep a 200/503 readiness contract they understand.

## Decision
- `/health` — public **readiness**. The Npgsql check runs behind a process-wide
  `CachedHealthCheck` with a **10 s TTL** and single-flight coalescing. The
  anonymous body is only `{"status":"<Healthy|Degraded|Unhealthy>"}`. Default
  status codes remain 200 for Healthy/Degraded and 503 for Unhealthy. It
  carries the named `health` policy.
- `/health/live` — public **liveness**. `Predicate = _ => false`, so no check
  runs and no database connection is opened. Status-only body. No rate limit.
- `/health/details` — **SystemAdmin-only** full report (check name, status,
  description). Same cached database check and the same `health` policy.
- Npgsql connection timeout **3 s**; health-check registration timeout **5 s**;
  `FailureStatus = Unhealthy`; tags `["db", "postgresql"]`.
- The named `health` policy is a fixed window of **60 requests per minute per
  normalized client IP** (`QueueLimit = 0`). The existing rejection handler
  returns `429` with `Retry-After: 60`.
- `CachedHealthCheck` is registered as a **DI singleton** and resolved by the
  health-check registration factory.

## Rationale
- **Why a server-side cache.** `HealthCheckOptions.AllowCachingResponses` only
  controls HTTP cache headers; it does not cache a result on the server. A
  request flood would still run one check per request. The cache is the
  control that actually bounds PostgreSQL connections.
- **Why a wrapper and not `IHealthCheckPublisher`.** The publisher evaluates on
  a fixed period (30 s by default) even with no traffic. The wrapper evaluates
  on demand, coalesces concurrent callers, and lets the first request after
  expiry pay the check cost. It also keeps the health result fresh enough for a
  readiness monitor (staleness ≤ 10 s) without a background hosted service.
- **Why the cache must be a singleton.** `DefaultHealthCheckService` invokes
  `registration.Factory` with a fresh DI scope on every evaluation. A cache
  constructed inside that factory is per-request and would silently restore
  the original SEC-11 behavior — all unit tests would still pass while every
  request opened a connection.
- **Why status-only for anonymous callers.** Probes only read the status line.
  Check names and exception text add no monitoring value and disclose
  infrastructure detail. Operators retain diagnostics on `/health/details`
  behind the existing `SystemAdmin` policy.
- **Why 60/min and why liveness is exempt.** The cache already bounds database
  load; the limiter is an abuse brake for request floods, not the primary
  control. Kubernetes and most uptime monitors treat any status ≥ 400 as a
  probe failure, so a tight limit on a probe can cause restart storms or false
  alarms. 60/min is far above normal probe cadence (container 2/min, monitor
  1/min), and `/health/live` remains unlimited so a flood cannot make the
  liveness signal flap.

## Consequences
- A database outage is reflected on `/health` within at most 10 s; after that
  the cached Unhealthy result serves 503 until the next expiry/refresh.
- The cache is per process. With N replicas, each replica opens at most one
  health connection per 10 s — acceptable; no distributed cache is introduced.
- Behind a reverse proxy, per-IP limiting uses `ForwardedHeaders` only when
  `SD_KNOWN_PROXIES` is set. If it is unset, every proxied client shares one
  bucket, so a flood can 429 an uptime monitor along with the attacker. Set
  `SD_KNOWN_PROXIES` (documented in `docs/self-hosting.md`).
- Rate limiting is not DDoS protection; volumetric attacks belong at the edge.

## Residual risks
- `/health/details` returns the Npgsql failure description to SystemAdmins. A
  forged or stolen admin token already implies broader access; the detailed
  body is not exposed anonymously.
- The health check uses a connection string with `Timeout=3` rather than the
  EF Core pool. The cache limits it to one connection attempt per 10 s, so the
  extra pool is not a material cost.
- The cache uses wall-clock `TimeProvider`; production clocks do not step
  backwards. In tests the fake clock is reset per test, which can extend a
  cached entry until the next advance; no test depends on cross-request
  freshness.

## Sources
- Microsoft Learn, [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0) — `AllowCachingResponses` semantics, publisher defaults, endpoint-aware `MapHealthChecks`, tagged liveness/readiness.
- Microsoft Learn, [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0) — endpoint policies, IP-spoofing and DDoS limitations.
- Xabaril, [`NpgSqlHealthCheck`](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks/blob/master/src/HealthChecks.NpgSql/NpgSqlHealthCheck.cs) — opens a connection per invocation and returns a failure result on exceptions/timeouts.
- Kubernetes, [Liveness, Readiness, and Startup Probes](https://kubernetes.io/docs/concepts/workloads/pods/probes/) — dependency checks belong in readiness; non-2xx/3xx is a probe failure.
- CWE-200 (information exposure) and CWE-400 (resource exhaustion).
