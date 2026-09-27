# Releasing

Releases follow semantic versioning and are driven by `scripts/bump-version.sh`, which orchestrates [commit-and-tag-version](https://github.com/absolute-version/commit-and-tag-version) (bumps `package.json` + `VERSION`) and [git-cliff](https://git-cliff.org) (changelog), then commits, tags `vX.Y.Z`, and pushes to trigger the GitLab CI pipeline.

```bash
./scripts/bump-version.sh patch    # or minor / major
./scripts/bump-version.sh --auto   # derive bump from Conventional Commits since last tag
./scripts/bump-version.sh patch -d # dry-run preview
```

The changelog is generated from Conventional Commits — `feat`, `fix`, `refactor`, `style`, `build`, etc. `chore` and `docs` commits are excluded. To regenerate the full `CHANGELOG.md` from all past tags:

```bash
pnpm install   # one-time, installs git-cliff + commit-and-tag-version at repo root
pnpm changelog
```

For the full release process, CI pipeline details, and gotchas, see [`docs/agents/release.md`](../agents/release.md) and [`docs/agents/ci.md`](../agents/ci.md).

## Release candidates

Release candidates are cut with `./scripts/bump-rc.sh [--base X.Y.Z] [-d] [-y]`, which creates and pushes an annotated `vX.Y.Z-rc.N` tag. RC tags are tag-only — no `VERSION`/`package.json` change, no commit. CI publishes Docker tags `:X.Y.Z-rc.N` (immutable) plus a moving `:rc` tag (never `:latest`); no GitLab or GitHub release objects are created. See [`docs/agents/release.md`](../agents/release.md) for details.