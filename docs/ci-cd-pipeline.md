# CI/CD Pipeline

SplitDuo uses GitLab CI/CD exclusively (no GitHub Actions). The pipeline is composed from a root `.gitlab-ci.yml` that includes per-concern files under `ci/`.

## Stages

```
verify → test → coverage → build → release
```

Each stage gates the next. A job in `build` only starts after every `verify` job passes; `coverage` aggregates the reports produced by `test`; `release` runs last, after the preceding stages (including `build`) succeed.

| Stage | Purpose | Jobs |
|---|---|---|
| `verify` | Quality gates — lint, typecheck, security scans, unit tests | `lint`, `typecheck`, `frontend-unit-tests`, `frontend-contract`, `unit-tests`, `security:trivy`, `security:trufflehog` |
| `test` | Integration tests (Testcontainers + PostgreSQL) | `integration-tests` |
| `coverage` | Aggregate unit + integration coverage | `aggregate-coverage` |
| `build` | Multi-arch Docker image build + push | `build_amd64`, `build_arm64`, `merge_manifest` |
| `release` | GitLab + GitHub release creation | `extract_release_notes`, `create_gitlab_release`, `create_github_release` |

## Job trigger matrix

| Job | Stage | Version tag `vX.Y.Z` | RC tag `vX.Y.Z-rc.N` | Push to `main` | Other branch | MR |
|---|---|---|---|---|---|---|
| `lint` | verify | auto | auto | auto | — | auto |
| `typecheck` | verify | auto | auto | auto | — | auto |
| `frontend-unit-tests` | verify | auto | auto | auto | — | auto |
| `frontend-contract` | verify | auto | auto | auto | — | auto |
| `unit-tests` | verify | auto | auto | auto | — | auto |
| `security:trivy` / `security:trufflehog` | verify | auto | auto | auto | manual (allow_failure) | manual (allow_failure) |
| `integration-tests` | test | auto | auto | auto | manual (allow_failure) | manual (allow_failure) |
| `aggregate-coverage` | coverage | auto | auto | auto | — | manual (allow_failure) |
| `build_amd64` / `build_arm64` / `merge_manifest` | build | auto | auto (rc tags only, never `:latest`) | — | manual | — |
| `extract_release_notes` | release | auto | — | — | — | — |
| `create_gitlab_release` | release | auto | — | — | — | — |
| `create_github_release` | release | auto | — | — | — | — |

### Release candidate flow

```
RC tag vX.Y.Z-rc.N pushed (by scripts/bump-rc.sh)
  │
  ├─ verify stage: lint, typecheck, frontend tests, unit-tests, security scans
  ├─ test stage:   integration-tests
  ├─ coverage:     aggregate-coverage
  │
  ├─ build stage:  build_amd64 + build_arm64 → merge_manifest
  │                pushes :X.Y.Z-rc.N + moving :rc  (never :latest)
  │
  └─ release stage: no jobs match — skipped
```

The RC tag pipeline runs the full quality gate but stops before the release stage. RC visibility is limited to git tags, Docker Hub tags, and the README section — no GitLab or GitHub release objects are created, so the GitLab release badge keeps showing the last stable release.

## Pipeline flows

### Release flow (version tag pushed by `scripts/bump-version.sh`)

```
tag vX.Y.Z pushed
  │
  ├─ verify stage: lint, typecheck, frontend tests, unit-tests, security scans
  ├─ test stage:   integration-tests (Testcontainers + PostgreSQL)
  ├─ coverage:     aggregate-coverage (unit + integration)
  │
  ├─ build stage:  build_amd64 + build_arm64 → merge_manifest
  │                pushes :X.Y.Z + :latest multi-arch manifests
  │
  ├─ extract_release_notes (CHANGELOG.md section)           ┐ release stage
  ├─ create_gitlab_release (native release-cli)             ├─ needs extract_release_notes
  └─ create_github_release (GitHub Releases API)            ┘
```

This is the only flow where every stage runs. The tag pipeline is the full release verification + publication path.

### Main branch flow (regular commit, no tag)

```
push to main (no tag)
  │
  ├─ verify stage: lint, typecheck, frontend tests, unit-tests, security scans
  ├─ test stage:   integration-tests
  ├─ coverage:     aggregate-coverage
  │
  (build stage: build jobs have no stable/RC tag → not created)
  (release stage: no jobs match — skipped)
```

`verify`, `test`, and `coverage` run on every main push so the coverage badge reflects real aggregate coverage. Build and release stay off main to conserve CI quota — main pushes do not build Docker images.

### Merge request flow

```
MR opened/updated
  │
  ├─ verify stage: lint, typecheck, frontend tests, unit-tests (all auto)
  │                security scans (manual, allow_failure)
  │
  (test stage: integration-tests manual, allow_failure)
  (coverage stage: aggregate-coverage manual, allow_failure)
```

MR pipelines run the automatic `verify` jobs; security scans, integration tests, and aggregate coverage are available on manual trigger with `allow_failure: true`.

### Feature branch flow (non-main, non-tag, non-MR)

```
push to feature branch
  │
  (verify stage: verify jobs have no rule for plain feature branches — skipped)
  (security scans / integration-tests: manual, allow_failure)
  (build stage: build_amd64 + build_arm64 + merge_manifest — manual, builds :<branch>)
```

Pipelines on feature branches are opt-in: security scans, integration tests, and the Docker build are all manual. This avoids consuming CI quota for pushes that don't need verification.

## Design decisions

### Why a `verify` stage instead of folding checks into `build`

Separating `verify` from `build` enforces the quality gate before any Docker image is built. Stage ordering guarantees the build jobs cannot start until `lint` and `unit-tests` pass. This follows the shift-left principle: catch problems before the expensive Docker build step.

### Why the badge is pinned to `main` and labeled "build status"

GitLab has no native "latest release/tag pipeline status" badge — badges are branch-pinned. The `main` badge reflects the health of the branch releases are cut from. With the `verify` stage running on every main push, the badge now represents real branch health ("main is releasable") rather than an empty pipeline.

The label was changed from "pipeline status" to "build status" to honestly represent what the badge shows. The third README badge ("latest version") tracks the actual released version, so the combination — build status (main) + latest version (release) — tells the full story.

### Why integration tests are auto on tags but manual elsewhere

Integration tests use Testcontainers with a real PostgreSQL container, which is slower and consumes more CI quota than unit tests. Running them automatically on every main push would burn through the GitLab CI quota quickly. On the release path (version tags), they run automatically as a release gate — a broken release cannot ship. Elsewhere they remain available as a manual trigger with `allow_failure: true`.

### Why the build jobs have no `needs:` clause

Stage ordering (`verify` → `build`) already enforces that the build jobs wait for `lint` and `unit-tests` to pass. `merge_manifest` declares `needs: [build_amd64, build_arm64]` so it only runs once both per-arch builds are complete. This keeps the job definitions simpler.

## CI quota considerations

GitLab shared runners have monthly minute quotas. The current design minimizes quota usage:

- **Main pushes**: `verify` + `test` + `coverage` (no Docker).
- **MRs**: automatic `verify` jobs; security/integration/coverage manual.
- **Feature branches**: nothing runs automatically.
- **Stable tags (releases)**: full pipeline — builds Docker images and creates releases.
- **RC tags**: `verify` + `test` + `coverage` + `build` (rc images only) — no release stage.

If quota becomes a concern, the first lever is making `integration-tests` on tags manual again (revert `ci/integration-tests.yml` rules to the previous manual-only config). The `verify` stage is cheap and should stay automatic.

## File layout

```
.gitlab-ci.yml          # stages + includes
ci/
  verify.yml            # lint, typecheck, frontend tests, unit-tests (verify stage)
  security.yml          # security:trivy, security:trufflehog (verify stage)
  build.yml             # build_amd64, build_arm64, merge_manifest (build stage)
  integration-tests.yml # integration-tests (test stage)
  coverage.yml          # aggregate-coverage (coverage stage)
  release.yml           # extract_release_notes + create_gitlab_release + create_github_release (release stage)
```

Each CI concern is a separate file under `ci/` and included from the root `.gitlab-ci.yml`. Add new CI concerns as new files under `ci/` and include them here.

## Required CI variables

Set these in GitLab → Settings → CI/CD → Variables. Two independent flags matter here — do not conflate them:

- **Protected** — a protected variable is only injected into pipelines running on a **protected ref** (branch or tag). This is what gates *visibility*.
- **Masked** — only redacts the value from job logs. It has no effect on which refs can see the variable.

| Variable | Required by | Purpose |
|---|---|---|
| `DOCKER_HUB_USERNAME` | build jobs | Docker Hub login |
| `DOCKER_HUB_PASSWORD` | build jobs | Docker Hub login (masked) |
| `GITHUB_TOKEN` | `create_github_release` | GitHub PAT with `repo` scope, or fine-grained with `Contents: read` + `Releases: write` on `c4mbr0nn3/splitduo` (masked, protected) |

No CI variables are needed for `verify` or `integration-tests` — the integration test job uses a hardcoded JWT secret for its ephemeral Testcontainers database.

RC tag pipelines run `docker login` too, so `DOCKER_HUB_USERNAME`/`DOCKER_HUB_PASSWORD` must be visible to them. If those variables are **protected**, then the RC tags must match a **protected-tag pattern** or `docker login` will receive empty credentials. GitLab protected-tag patterns are glob-style (not regex): use `v*-rc.*` with "Allowed to create" = Maintainers. If the Docker Hub variables are **not** protected, they resolve on any ref and no tag protection is needed.

> Verify which applies in your instance: **Settings → CI/CD → Variables** shows each variable's Protected/Masked flags. A masked-*and-protected* variable does surface on a protected tag such as `v1.17.0-rc.1` (that is exactly how `GITHUB_TOKEN` works on stable tags today) — masking is not what blocks it.

## Release process

Releases are orchestrated by `scripts/bump-version.sh`, which:

1. Bumps `VERSION` + `package.json` via `commit-and-tag-version`.
2. Generates the changelog entry via `git-cliff` and amends the commit.
3. Creates an annotated `vX.Y.Z` tag.
4. Pushes the commit + tag to `gitlab.com/j1mm0/splitduo`.

The tag push triggers the release pipeline flow (above). The GitLab mirror propagates the tag to `github.com/c4mbr0nn3/splitduo` automatically, so `create_github_release` only needs to create the release object via the API.

For backfilling releases on pre-existing tags (before the CI release jobs existed), use `scripts/backfill-releases.sh`.

Release candidates are separate: `scripts/bump-rc.sh` creates and pushes a tag-only `vX.Y.Z-rc.N` tag (no `VERSION`/`package.json` change, no commit). RC pipelines build and publish `:X.Y.Z-rc.N` + `:rc` images but never create GitLab/GitHub releases. The stable flow above is unchanged.