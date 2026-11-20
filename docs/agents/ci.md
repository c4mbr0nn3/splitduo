# CI Guide

> Read this before touching `ci/*.yml`, `.gitlab-ci.yml`, or CI scanner images.

## Structure

- CI lives in `ci/*.yml` — one file per concern, included from `.gitlab-ci.yml`.
- Add new CI concerns as a new file under `ci/` and include it in `.gitlab-ci.yml`.
- Stage order: `verify` (lint, typecheck, security scans) → `test` (unit + integration tests, emit per-suite coverage) → `coverage` (aggregate-coverage, owns the pipeline `coverage:` regex) → `build` (docker) → `release` (GitLab + GitHub releases).

## Runner Routing

- Most jobs are tagged `homelab` (private runner) so they never consume GitLab.com shared compute minutes.
- The Docker build jobs are the exception: `build_amd64` runs on `saas-linux-small-amd64` and `build_arm64` on `saas-linux-small-arm64`. The homelab runner has a degenerate cpuset that triggers a .NET 10.0.12 Server GC init failure (`Failed to create CoreCLR, HRESULT: 0x8013200A` / "affinitize to 0 CPUs") during `dotnet publish`, and it is amd64-only so a native ARM builder is required regardless. Revisit once the SDK bundles a runtime with the fix (dotnet/runtime#133497, expected 10.0.13).
- There is **no runner fallback**: a job whose tag is not served by an online runner stays pending until that runner returns. Untagged jobs would silently run on shared runners and burn minutes.
- New jobs under `ci/` must declare `tags: [homelab]` unless they genuinely need a SaaS runner. Do not rely on `.build_template` — it carries no tags, so each concrete build job must set its own.

## Image Pinning

- CI scanner/third-party images are pinned by digest (`image@sha256:...`) for supply-chain integrity.
- Bump the digest deliberately **after verifying the upstream image** — never use a bare mutable tag.

## Runner Disk Capacity

The `homelab` runner shares one filesystem between every job container and all concurrent pipelines. Two jobs are GB-scale consumers:

- `security:trivy` downloads a vulnerability-DB artifact (~135 MB) and unpacks it to ~1.4 GB of sqlite, so a scan peaks near 1.6 GB. The `aquasec/trivy` image ships **no** DB, so every run pays this cost — `--skip-db-update` would fail on the first run, not save space.
- `integration-tests` starts a `docker:28-dind` service, pulls Postgres and writes test databases.

Both declare `resource_group: homelab-disk-heavy`. Resource groups are mutually exclusive **across pipelines of the project**, so a tag pipeline's Trivy gate cannot run while another pipeline's dind job fills the same disk. Jobs hold the group while queued, so a release gate can wait behind another pipeline's integration run (≈5 min in the worst case); intra-pipeline cost is zero because the two jobs are in different stages anyway.

Both also preflight free space and fail below 1.5 GiB with an explicit runner-capacity message. The threshold is deliberately under what a GB-scale job needs, and under the ~2.3 GB the host normally has free (measured: 11.7 GB total, 8.8 GB used), so a healthy-but-busy host still runs the job and only a genuine shortfall trips the check. That check is the signal for the ops fix — keep headroom on the runner host (`docker system prune`, image GC) or lower runner `concurrency` — and not a repo change: a full disk will still break whichever job runs next.

Incident record: v1.18.0-rc.2 failed with twin ENOSPC on the same second — `security:trivy` job 18224042957 (`no space left on device` mid-DB-write) and `integration-tests` job 18223992325 (Postgres 53100 inside the test run). If ENOSPC recurs after serialization, escalate by moving only `security:trivy` to `saas-linux-small-amd64` (30 GB disk, privileged, arbitrary jobs allowed) as a documented exception like `build_amd64`; digest pinning carries over unchanged.

## Gotchas

- `ci/*.yml` is the per-concern split; `.gitlab-ci.yml` is the include root. Don't inline new pipeline concerns into `.gitlab-ci.yml` — add a new file under `ci/` and include it.
- `no space left on device` in a homelab job is a shared-runner disk issue, not a code bug — see Runner Disk Capacity. `resource_group: homelab-disk-heavy` must stay on both GB-scale jobs; removing it restores the cross-pipeline race.
- Scanner/third-party images must stay digest-pinned; never replace a digest with a mutable tag like `:latest`.
- Tag rules match stable `vX.Y.Z` and/or RC `vX.Y.Z-rc.N` explicitly; do not reintroduce the old permissive regex.
- `ci/build.yml`'s `merge_manifest` decides tags with an explicit mutually-exclusive branch: RC → `:X.Y.Z-rc.N` + `:rc` (never `:latest`); stable → `:X.Y.Z` + `:latest`. Per-arch temp tags are namespaced with `$CI_PIPELINE_ID`.
- RC tags must be protected tags in GitLab (`v*-rc.*` glob pattern, "Allowed to create": Maintainers) so protected CI variables resolve on the RC pipeline.