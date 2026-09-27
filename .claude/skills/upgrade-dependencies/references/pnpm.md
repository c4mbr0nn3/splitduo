# pnpm upgrade notes

Consult this when bumping frontend or root tooling packages.

## Two pnpm areas

| Area | Manifest | Lockfile | Role |
|---|---|---|---|
| Root | `package.json` | `pnpm-lock.yaml` | Release tooling (`git-cliff`, `commit-and-tag-version`) — dev only, not a workspace root and not shipped. |
| Frontend | `sd-frontend/package.json` | `sd-frontend/pnpm-lock.yaml` | The shipped Nuxt app. |

Both pin `pnpm@11.11.0`. Neither is a workspace, so there is no hoisted catalog to route through — run `pnpm` in the area you are bumping.

A third pnpm project exists at `website/` (Astro marketing site), but it is a **separate gitignored repo** (`.gitignore`: `website/`). It is out of scope for this repo's bumps.

## Where pnpm settings live

Since pnpm 10, general settings moved out of `.npmrc` into `pnpm-workspace.yaml`. pnpm 11 reads it even in a non-workspace project.

- `sd-frontend/pnpm-workspace.yaml` — `supportedArchitectures`, `overrides`, `allowBuilds`.
- `sd-frontend/.npmrc` — registry/auth territory only (`shamefully-hoist`, `strict-peer-dependencies`).
- No root `pnpm-workspace.yaml` and no root `.npmrc`.

## Supply chain

The `minimumReleaseAge` gate is pnpm 11's built-in default, and it applies to direct and transitive packages alike. It is deliberately non-strict: pnpm falls back to a too-new version only when nothing in range satisfies the window.

The consequence for this repo is that `minimumReleaseAgeExclude` is **live without any repo file setting `minimumReleaseAge`**. The only exclusion in the ecosystem today lives in the separate `website/` repo (`astro@7.2.1`); `pnpm audit --fix` also appends patched versions here to let security fixes through immediately. When a bump surfaces an exclusion in a manifest you touch, its job is done once the fix has aged past the window — drop it then.

## Lockfile review

`pnpm update` rewrites the lockfile, and transitive packages can move silently inside a bump that looks mechanical. After bumping, read `git diff pnpm-lock.yaml` to see what actually changed, then triage any package that moved *itself* on its own merits — the manifest entry is not the only thing that changed.

## Frozen during a bump

Keep these as-is and change them deliberately, separately from a package bump:

- **`overrides`** — `sd-frontend/pnpm-workspace.yaml` pins `nanoid: 3.3.18`. Leave pins and widenings to their own change.
- **`allowBuilds`** — `esbuild`, `sharp`, `unrs-resolver`, `@parcel/watcher`, `vue-demi` are approved to run install scripts. A major on any of these can fail the build through a native-binary mismatch that looks unrelated.
- **`supportedArchitectures`** — keep `arm64`/`x64`/`linux` listed; dropping one breaks installs on that target.

## Cascade

A bump that changes the backend OpenAPI spec means regenerating the frontend types — see `references/dotnet.md`. `pnpm test:contract` is the CI-only staleness check for that.
