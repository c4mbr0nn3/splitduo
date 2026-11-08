# Releasing

Releases follow semantic versioning, and `scripts/bump-version.sh` drives them. The script uses [commit-and-tag-version](https://github.com/absolute-version/commit-and-tag-version) to bump `package.json`, `VERSION`, and `sd-frontend/package.json`. It then syncs `sd-backend/Directory.Build.props` and `docs/api/splitduoapi-v1.yaml` to the new version, and uses [git-cliff](https://git-cliff.org) to generate the changelog entry. It commits, adds the changelog entry to that commit, tags `vX.Y.Z`, and pushes to trigger the GitLab CI pipeline.

```bash
./scripts/bump-version.sh patch    # or minor / major
./scripts/bump-version.sh --auto   # derive bump from Conventional Commits since last tag
./scripts/bump-version.sh patch -d # dry-run preview
```

`git-cliff` generates the changelog from Conventional Commits: `feat`, `fix`, `refactor`, `style`, `build`, etc. It excludes `chore` and `docs` commits. To regenerate the full `CHANGELOG.md` from all past tags:

```bash
pnpm install   # one-time, installs git-cliff + commit-and-tag-version at repo root
pnpm changelog
```

For the full release process, CI pipeline details, and gotchas, see [`docs/agents/release.md`](../agents/release.md) and [`docs/agents/ci.md`](../agents/ci.md).

## Release candidates

Create release candidates with `./scripts/bump-rc.sh [--base X.Y.Z] [-d] [-y]`. The script creates and pushes an annotated `vX.Y.Z-rc.N` tag. RC tags are tag-only: no `VERSION`/`package.json` change, no commit. CI publishes Docker tags `:X.Y.Z-rc.N` (immutable) plus a moving `:rc` tag (never `:latest`). CI creates no GitLab or GitHub release objects. See [`docs/agents/release.md`](../agents/release.md) for details.