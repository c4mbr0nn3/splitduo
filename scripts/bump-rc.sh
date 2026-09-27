#!/bin/bash

# Create and push a release-candidate tag (vX.Y.Z-rc.N).
#
# RC tags are TAG-ONLY: this script never commits and never modifies VERSION,
# package.json, or any other file. The RC image version comes from the tag
# itself (see ci/build.yml), so the working tree stays on the last stable
# version. This keeps scripts/bump-version.sh unchanged and guarantees the
# stable release path is unaffected.
#
# Usage: ./scripts/bump-rc.sh [--base X.Y.Z] [-y|--yes] [-d|--dry-run] [-h|--help]
#
# --base defaults to the next stable version derived from
# `git cliff --bumped-version` (Conventional Commits since the last stable tag).
# The RC number is the next free N for that base (vX.Y.Z-rc.0, -rc.1, ...).

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

DRY_RUN=false
AUTO_CONFIRM=false
BASE=""

print_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Create and push a release-candidate tag vX.Y.Z-rc.N (tag-only, no commits).

Options:
  --base X.Y.Z    Base version for the RC (default: next stable version)
  -d, --dry-run   Preview the tag without creating or pushing it
  -y, --yes       Skip the confirmation prompt
  -h, --help      Display this help message

Examples:
  $0                       # RC for the next derived stable version
  $0 --base 1.17.0         # RC for 1.17.0
  $0 --base 1.17.0 -d      # Preview the next 1.17.0 RC tag
EOF
    exit 0
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --base)
            if [[ -z "$2" ]]; then
                echo -e "${RED}Error: --base requires a version argument${NC}"
                exit 1
            fi
            BASE="$2"
            shift 2
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -y|--yes)
            AUTO_CONFIRM=true
            shift
            ;;
        -h|--help)
            print_usage
            ;;
        *)
            echo -e "${RED}Error: Unknown argument '$1'${NC}"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Resolve the base version, defaulting to the next stable version.
# grep filters to the version token: pnpm may print its own stdout banner
# (e.g. "Done in 0.3s") on first run, which would otherwise be captured.
if [[ -z "$BASE" ]]; then
    BASE=$(pnpm exec git-cliff --bumped-version 2>/dev/null \
        | grep -oE 'v?[0-9]+\.[0-9]+\.[0-9]+' | tail -1 | sed 's/^v//')
fi

if [[ ! "$BASE" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}Error: could not determine a valid base version (got '$BASE')${NC}"
    echo "Pass one explicitly, e.g.: $0 --base 1.17.0"
    exit 1
fi

# Determine the next free RC number for this base from BOTH local and remote
# tags. A stale local clone must not reuse a number already published to
# origin (which would silently skip an RC number). `--refs` omits peeled
# `^{}` entries so the numeric suffix parses cleanly.
git fetch --tags origin >/dev/null 2>&1 || true
LATEST_LOCAL=$(git tag --list "v${BASE}-rc.*" --sort=-v:refname | head -1)
LATEST_REMOTE=$(git ls-remote --tags --refs origin "v${BASE}-rc.*" 2>/dev/null \
    | sed -n 's#.*refs/tags/##p' | sort -V | tail -1)
LATEST_RC=""
for candidate in "$LATEST_LOCAL" "$LATEST_REMOTE"; do
    [[ -z "$candidate" ]] && continue
    if [[ -z "$LATEST_RC" ]] || (( ${candidate##*-rc.} > ${LATEST_RC##*-rc.} )); then
        LATEST_RC="$candidate"
    fi
done
if [[ -z "$LATEST_RC" ]]; then
    RC_NUMBER=0
else
    RC_NUMBER=$(( ${LATEST_RC##*-rc.} + 1 ))
fi
TAG="v${BASE}-rc.${RC_NUMBER}"

# Refuse to tag a dirty working tree (the tag would point at a commit that
# does not include the uncommitted changes, which is confusing on an RC).
if [[ -n $(git status --porcelain | grep -v "^?? ") ]]; then
    echo -e "${RED}Error: You have uncommitted changes. Commit or stash them first.${NC}"
    git status --short
    exit 1
fi

# Collision checks (local and remote).
if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo -e "${RED}Error: Tag $TAG already exists locally${NC}"
    exit 1
fi
if git ls-remote --tags --refs origin 2>/dev/null | grep -q "refs/tags/$TAG$"; then
    echo -e "${RED}Error: Tag $TAG already exists on remote${NC}"
    exit 1
fi

echo -e "${YELLOW}Release candidate:${NC}"
echo "  Base:   $BASE"
echo "  RC #:   $RC_NUMBER"
echo "  Tag:    $TAG"
echo "  HEAD:   $(git rev-parse --short HEAD) $(git log -1 --pretty=%s)"
echo ""
echo "This will create an annotated tag and push it to origin."
echo "It will NOT modify VERSION/package.json and will NOT create a commit."
echo "CI will publish Docker images :$BASE-rc.$RC_NUMBER and :rc (never :latest)."
echo ""

if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY-RUN MODE]${NC} No tag created or pushed."
    exit 0
fi

if [[ "$AUTO_CONFIRM" != true ]]; then
    read -p "Continue? [y/N] " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Aborted by user${NC}"
        exit 0
    fi
fi

git tag -a "$TAG" -m "Release candidate $TAG"
echo -e "${GREEN}✓${NC} Created tag $TAG"

git push origin "$TAG"
echo -e "${GREEN}✓${NC} Pushed tag $TAG to remote"

echo ""
echo -e "${GREEN}🎉 Release candidate tagged!${NC}"
echo -e "Version: ${GREEN}${BASE}-rc.${RC_NUMBER}${NC}"
echo ""
echo "Once the pipeline passes, set image: j1mm0/splitduo:rc in docker-compose.yml and run: docker compose pull && docker compose up -d"
echo "When the RC is validated, cut the stable release with: ./scripts/bump-version.sh"