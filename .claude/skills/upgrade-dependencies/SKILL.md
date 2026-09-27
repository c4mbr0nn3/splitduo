---
name: upgrade-dependencies
description: Bump, upgrade, or update existing dependencies across the SplitDuo repo — the .NET 10 backend (NuGet), the pnpm frontend, and root release tooling. Use whenever asked to bump or update packages, check for outdated dependencies, or migrate a package across a breaking major. For first-time adoption of a library the project has never used, use source-driven-development.
---

# Dependency Upgrades

Every bump runs one of two lanes; triage picks the lane. Both lanes end on the same floor.

## 1. Scan and triage

Enumerate direct dependencies and their deltas:

- **Backend** — from `sd-backend/`: `dotnet list sd-backend.sln package --outdated` (one solution, four projects).
- **pnpm** — `pnpm outdated` in the root (release tooling) and in `sd-frontend/`.

Classify each package before touching anything:

- **mechanical** — a patch/minor inside the declared range, on a package whose behavior the code does not depend on at runtime. Lockfile churn from `pnpm update` lands here.
- **breaking** — a semver major, a runtime-critical package, an ecosystem that ships breaking changes in minors (`nuxt`, `nuxt ui`, `astro`), or a surface with no test coverage.

The split exists because the failure modes differ: a mechanical bump fails loudly at build or typecheck, while a breaking bump can pass the build and still change runtime behavior. Runtime-critical means a package on the auth path (JWT, 2FA), the data path (EF Core, Npgsql, CsvHelper), or a native build (`esbuild`, `sharp`, `unrs-resolver`).

Done when every direct package carries a lane and a one-line reason.

## 2. Mechanical lane

Bump with the ecosystem tool, run the area's gate set from §4, then commit.

## 3. Breaking lane

Escalate a package here when triage calls it breaking.

- Map the **contract** — the exact APIs, entry points, and package IDs the code depends on. Delegate the usage map for anything non-trivial.
- Research the migration from primary sources; treat prose docs as hypotheses.
- Prove it in a **throwaway copy** outside the workspace — `cp -r sd-backend /tmp/upgrade-<pkg>` (or the `sd-frontend` area), then bump, build, fix, and test there. Read the lockfile diff in the copy to see what actually moved. *Proven* means the area's build, typecheck, and every test it has are green in the copy.
- Verify the contract **from the artifact** — reflected signatures, real exports maps, release diffs — rather than trusting the changelog alone.
- When the surface has no test coverage, build a runtime probe and exercise it in a real browser or runtime.

Done when the copy is green and the contract is confirmed from the artifact, not the docs.

## 4. The floor (both lanes)

- **Backend gate** — from `sd-backend/`: `dotnet build`, then `dotnet test SplitDuo.Tests.Unit/SplitDuo.Tests.Unit.csproj`, then `./run-integration-tests.sh`. The integration runner needs Docker/Podman and Testcontainers; run it whenever the bump touches code its existing tests cover (EF Core and Npgsql are the common cases).
- **Frontend gate** — from `sd-frontend/`: `pnpm lint:fix`, `pnpm typecheck`, `pnpm test`.
- **Contract verified** — from the artifact wherever possible.
- **Supply-chain gate** — pnpm 11 enables `minimumReleaseAge` (1440 minutes) by default, so a version published within the last day is withheld unless listed under `minimumReleaseAgeExclude`. Treat an exclusion as a deliberate bypass of that delay: prefer latest-minus-one when the newest version carries no relevant fix, and remove an exclusion once its fix has aged past the window. See `references/pnpm.md` for where the setting lives.
- Commit only after the gates pass and the user approves; record a non-obvious call in the commit subject. When a gate goes red, revert the manifest and lockfile together and stop rather than fixing forward in the same commit.

## Delegation

- explorer → contract/usage map
- librarian → migration research
- oracle → plan or validate a broad breaking bump
- fixer → bounded mechanical edits

## References

- `references/dotnet.md` — NuGet restore, the four projects, unit/integration invocation, `PrivateAssets`, the OpenAPI regen cascade.
- `references/pnpm.md` — where pnpm settings live, lockfile review, `overrides`, native-build risk, the `gen:api` cascade.
