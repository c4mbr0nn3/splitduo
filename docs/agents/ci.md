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

## Gotchas

- `ci/*.yml` is the per-concern split; `.gitlab-ci.yml` is the include root. Don't inline new pipeline concerns into `.gitlab-ci.yml` — add a new file under `ci/` and include it.
- Scanner/third-party images must stay digest-pinned; never replace a digest with a mutable tag like `:latest`.
- Tag rules match stable `vX.Y.Z` and/or RC `vX.Y.Z-rc.N` explicitly; do not reintroduce the old permissive regex.
- `ci/build.yml`'s `merge_manifest` decides tags with an explicit mutually-exclusive branch: RC → `:X.Y.Z-rc.N` + `:rc` (never `:latest`); stable → `:X.Y.Z` + `:latest`. Per-arch temp tags are namespaced with `$CI_PIPELINE_ID`.
- RC tags must be protected tags in GitLab (`v*-rc.*` glob pattern, "Allowed to create": Maintainers) so protected CI variables resolve on the RC pipeline.