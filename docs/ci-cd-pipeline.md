# CI/CD Pipeline

SplitDuo uses GitLab CI/CD exclusively (no GitHub Actions). The pipeline is composed from a root `.gitlab-ci.yml` that includes per-concern files under `ci/`.

## Stages

```
verify → build → test → release
```

Each stage gates the next. A job in `build` only starts after every `verify` job passes; `release` only starts after `test` passes.

| Stage | Purpose | Jobs |
|---|---|---|
| `verify` | Quality gates — lint + unit tests | `lint`, `unit-tests` |
| `build` | Docker image build + push | `build_and_push` |
| `test` | Integration tests (Testcontainers + PostgreSQL) | `integration-tests` |
| `release` | GitLab + GitHub release creation | `extract_release_notes`, `create_gitlab_release`, `create_github_release` |

## Job trigger matrix

| Job | Stage | Version tag `vX.Y.Z` | Push to `main` | Other branch | MR |
|---|---|---|---|---|---|
| `lint` | verify | auto | auto | — | auto |
| `unit-tests` | verify | auto | auto | — | auto |
| `build_and_push` | build | auto | — | manual | — |
| `integration-tests` | test | auto | manual (allow_failure) | manual (allow_failure) | manual (allow_failure) |
| `extract_release_notes` | release | auto | — | — | — |
| `create_gitlab_release` | release | auto | — | — | — |
| `create_github_release` | release | auto | — | — | — |

## Pipeline flows

### Release flow (version tag pushed by `scripts/bump-version.sh`)

```
tag vX.Y.Z pushed
  │
  ├─ lint (frontend eslint)              ┐
  ├─ unit-tests (.NET xUnit + coverage)  ┘ verify stage
  │
  ├─ build_and_push (Docker build, push :X.Y.Z + :latest)   build stage
  │
  ├─ integration-tests (Testcontainers + PostgreSQL)        test stage
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
  ├─ lint        ┐
  └─ unit-tests  ┘ verify stage
  │
  (build stage: no jobs match — skipped)
  (test stage: integration-tests is manual, not triggered automatically)
  (release stage: no jobs match — skipped)
```

Only the `verify` stage runs. This is intentional: the `main` pipeline badge reflects real branch health (lint + unit tests pass) without consuming CI quota on Docker builds or integration tests for every commit. Integration tests can be triggered manually from the GitLab UI on main if needed.

### Merge request flow

```
MR opened/updated
  │
  ├─ lint        ┐
  └─ unit-tests  ┘ verify stage
  │
  (integration-tests: manual, allow_failure)
```

Same as main — `verify` runs automatically, integration tests are available on manual trigger.

### Feature branch flow (non-main, non-tag, non-MR)

```
push to feature branch
  │
  (verify stage: no rules match — skipped)
  (build_and_push: manual — can build a branch-tagged Docker image on demand)
  (integration-tests: manual, allow_failure)
```

Pipelines on feature branches are opt-in: `build_and_push` and `integration-tests` are both manual. This avoids consuming CI quota for pushes that don't need verification.

## Design decisions

### Why a `verify` stage instead of folding checks into `build`

Separating `verify` from `build` enforces the quality gate before any Docker image is built. Stage ordering guarantees `build_and_push` cannot start until `lint` and `unit-tests` pass. This follows the shift-left principle: catch problems before the expensive Docker build step.

### Why the badge is pinned to `main` and labeled "build status"

GitLab has no native "latest release/tag pipeline status" badge — badges are branch-pinned. The `main` badge reflects the health of the branch releases are cut from. With the `verify` stage running on every main push, the badge now represents real branch health ("main is releasable") rather than an empty pipeline.

The label was changed from "pipeline status" to "build status" to honestly represent what the badge shows. The third README badge ("latest version") tracks the actual released version, so the combination — build status (main) + latest version (release) — tells the full story.

### Why integration tests are auto on tags but manual elsewhere

Integration tests use Testcontainers with a real PostgreSQL container, which is slower and consumes more CI quota than unit tests. Running them automatically on every main push would burn through the GitLab CI quota quickly. On the release path (version tags), they run automatically as a release gate — a broken release cannot ship. Elsewhere they remain available as a manual trigger with `allow_failure: true`.

### Why `build_and_push` has no `needs:` clause

Stage ordering (`verify` → `build`) already enforces that `build_and_push` waits for `lint` and `unit-tests` to pass. Adding an explicit `needs:` would be redundant. This keeps the job definitions simpler.

## CI quota considerations

GitLab shared runners have monthly minute quotas. The current design minimizes quota usage:

- **Main pushes**: only `verify` stage (2 lightweight jobs, no Docker).
- **MRs**: only `verify` stage.
- **Feature branches**: nothing runs automatically.
- **Tags (releases)**: full pipeline — this is the only flow that builds Docker images and runs integration tests.

If quota becomes a concern, the first lever is making `integration-tests` on tags manual again (revert `ci/integration-tests.yml` rules to the previous manual-only config). The `verify` stage is cheap and should stay automatic.

## File layout

```
.gitlab-ci.yml          # stages + includes
ci/
  verify.yml            # lint + unit-tests (verify stage)
  build.yml             # build_and_push (build stage)
  integration-tests.yml # integration-tests (test stage)
  release.yml           # extract_release_notes + create_gitlab_release + create_github_release (release stage)
```

Each CI concern is a separate file under `ci/` and included from the root `.gitlab-ci.yml`. Add new CI concerns as new files under `ci/` and include them here.

## Required CI variables

Set these in GitLab → Settings → CI/CD → Variables (masked + protected where noted):

| Variable | Required by | Purpose |
|---|---|---|
| `DOCKER_HUB_USERNAME` | `build_and_push` | Docker Hub login |
| `DOCKER_HUB_PASSWORD` | `build_and_push` | Docker Hub login (masked) |
| `GITHUB_TOKEN` | `create_github_release` | GitHub PAT with `repo` scope, or fine-grained with `Contents: read` + `Releases: write` on `c4mbr0nn3/splitduo` (masked, protected) |

No CI variables are needed for `verify` or `integration-tests` — the integration test job uses a hardcoded JWT secret for its ephemeral Testcontainers database.

## Release process

Releases are orchestrated by `scripts/bump-version.sh`, which:

1. Bumps `VERSION` + `package.json` via `commit-and-tag-version`.
2. Generates the changelog entry via `git-cliff` and amends the commit.
3. Creates an annotated `vX.Y.Z` tag.
4. Pushes the commit + tag to `gitlab.com/j1mm0/splitduo`.

The tag push triggers the release pipeline flow (above). The GitLab mirror propagates the tag to `github.com/c4mbr0nn3/splitduo` automatically, so `create_github_release` only needs to create the release object via the API.

For backfilling releases on pre-existing tags (before the CI release jobs existed), use `scripts/backfill-releases.sh`.