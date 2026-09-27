---
name: upgrade-dependencies
description: Triage and execute dependency bumps across the SplitDuo monorepo (dotnet backend, pnpm frontend and root tooling). Use when asked to bump or update packages, check outdated dependencies, move a package to its latest version, or migrate a package across a breaking major. Covers semver triage into a mechanical lane and a breaking lane, contract-level verification, throwaway-copy experiments, runtime probes when tests do not cover the surface, and supply-chain gate handling. For first-time adoption of a new dependency, use source-driven-development instead.
---

# Dependency Upgrades

Every bump runs one of two lanes. Triage picks the lane; the floor is identical for both.

## 1. Scan and triage

- Enumerate direct dependencies and their deltas: `dotnet list <sln> package --outdated` (per solution/area), `pnpm outdated` (per manifest — root, `sd-frontend`).
- Classify each package:
  - **mechanical** — patch/minor inside the declared range.
  - **breaking** — semver major, OR the package sits on a runtime-critical path, OR the surface the code depends on has no test coverage.
- State the lane per package before touching anything.

## 2. Mechanical lane

- Bump with the ecosystem tool (`pnpm update` / `pnpm add`, or edit `.csproj` + restore).
- Run the area's full gate set — see `docs/agents/backend.md` and `docs/agents/frontend.md`.
- Commit.

## 3. Breaking lane

Escalate a package here when triage says breaking. Ceremony is recommended, not ritual — match it to the evidence you will actually gather.

- Map the **contract**: the exact API, entry points, and package IDs the code depends on. Delegate a usage map for anything non-trivial.
- Research the migration from primary sources; treat prose docs as hypotheses, not facts.
- Prove it in a **throwaway copy** outside the workspace: bump, build, fix, re-build.
- Verify the contract **from the artifact** — reflected signatures, real exports maps, release diffs — not from the changelog alone.
- When the surface has no test coverage, build a runtime probe and exercise it in a real browser or runtime.
- A strong artifact or probe can stand in for a plan; add a plan plus independent validation when the surface is broad or rollback is costly.

## 4. The floor (both lanes)

- The area's full gate set passes (lint, typecheck, tests; integration via the project's own runner).
- The specific contract is verified — from the artifact wherever possible.
- **Supply-chain gate**: a `minimumReleaseAge` exclusion is a bypass, not a convenience. Prefer latest-minus-one when the newest version carries no relevant fix, and drop stale bypass entries.
- Commit only after gates pass and with approval; record non-obvious decisions in the commit subject.

## Delegation

- explorer → usage/contract map
- librarian → migration research
- oracle → plan or validate a broad breaking bump
- fixer → bounded mechanical edits
