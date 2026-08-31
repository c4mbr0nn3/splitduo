#!/bin/bash

# Bump version script following semantic versioning.
#
# Orchestrates commit-and-tag-version (version bump of package.json + VERSION)
# and git-cliff (changelog generation from Conventional Commits).
#
# Usage: ./bump-version.sh [major|minor|patch|--auto] [-d|--dry-run] [-y|--yes] [-h|--help]
# Default: --auto (derive bump from Conventional Commits since last tag)
#
# The explicit bump type overrides commit-and-tag-version's auto-detection
# from Conventional Commits. Pass `--auto` (or no argument) to let cat-v
# derive the bump from commit history.

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# State tracking for rollback
DRY_RUN=false
AUTO_CONFIRM=false
COMMIT_CREATED=false
TAG_CREATED=false
ROLLBACK_ACTIVE=false

# Print usage information
print_usage() {
    cat << EOF
Usage: $0 [major|minor|patch|--auto] [OPTIONS]

Bump version following semantic versioning.

Arguments:
  major|minor|patch    Version component to bump (overrides auto-detection)
  --auto               Derive bump type from Conventional Commits since last tag
                       (delegates to commit-and-tag-version auto-detection)
                       DEFAULT when no bump type is given

Options:
  -d, --dry-run       Preview changes without making modifications
  -y, --yes           Skip confirmation prompt (auto-confirm)
  -h, --help          Display this help message

Examples:
  $0                  # Auto-derive bump from Conventional Commits (default)
  $0 --auto           # Same as above, explicit
  $0 patch            # Force patch bump (overrides auto-detection)
  $0 minor -y         # Force minor bump, auto-confirm
  $0 major -d         # Preview major version bump
EOF
    exit 0
}

# Parse arguments
BUMP_TYPE=""
while [[ $# -gt 0 ]]; do
    case "$1" in
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
        --auto)
            if [[ -n "$BUMP_TYPE" ]]; then
                echo -e "${RED}Error: Multiple bump types specified${NC}"
                exit 1
            fi
            BUMP_TYPE="auto"
            shift
            ;;
        major|minor|patch)
            if [[ -n "$BUMP_TYPE" ]]; then
                echo -e "${RED}Error: Multiple bump types specified${NC}"
                exit 1
            fi
            BUMP_TYPE="$1"
            shift
            ;;
        *)
            echo -e "${RED}Error: Unknown argument '$1'${NC}"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Default to auto (derive bump from Conventional Commits) if not specified
BUMP_TYPE="${BUMP_TYPE:-auto}"

# Check if VERSION file exists
VERSION_FILE="VERSION"
if [[ ! -f "$VERSION_FILE" ]]; then
    echo -e "${RED}Error: VERSION file not found${NC}"
    exit 1
fi

# Check if package.json exists (commit-and-tag-version reads version from it)
if [[ ! -f "package.json" ]]; then
    echo -e "${RED}Error: package.json not found${NC}"
    exit 1
fi

# Read current version (VERSION file is the source of truth on disk)
CURRENT_VERSION=$(cat "$VERSION_FILE" | tr -d '[:space:]')

# Validate version format
if [[ ! "$CURRENT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}Error: Invalid version format in VERSION file: $CURRENT_VERSION${NC}"
    echo "Expected format: X.Y.Z"
    exit 1
fi

# Build commit-and-tag-version release-as flag
CATV_RELEASE_ARG=""
if [[ "$BUMP_TYPE" == "auto" ]]; then
    # Let cat-v derive the bump from Conventional Commits since last tag
    CATV_RELEASE_ARG=""
else
    CATV_RELEASE_ARG="--release-as $BUMP_TYPE"
fi

# Compute the prospective new version for display and tag collision checks.
# We run cat-v in dry-run to get the bumped version without touching files.
# cat-v dry-run prints lines like:
#   "✔ bumping version in VERSION from 1.0.0\n to 1.0.1"
# We extract the version after "to ".
NEW_VERSION=$(pnpm exec commit-and-tag-version \
    $CATV_RELEASE_ARG \
    --skip.changelog --skip.commit --skip.tag \
    --dry-run 2>/dev/null \
    | tr -d '\n' \
    | grep -oE 'to [0-9]+\.[0-9]+\.[0-9]+' \
    | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' \
    | tail -1)

if [[ -z "$NEW_VERSION" ]]; then
    echo -e "${RED}Error: Could not determine new version from commit-and-tag-version dry-run${NC}"
    exit 1
fi

TAG_NAME="v$NEW_VERSION"

# Check for uncommitted changes
if [[ -n $(git status --porcelain | grep -v "^?? ") ]]; then
    echo -e "${RED}Error: You have uncommitted changes. Please commit or stash them first.${NC}"
    git status --short
    exit 1
fi

# Check if tag already exists locally
if git rev-parse "$TAG_NAME" >/dev/null 2>&1; then
    echo -e "${RED}Error: Tag $TAG_NAME already exists locally${NC}"
    exit 1
fi

# Check if tag already exists on remote
check_remote_tag() {
    if git ls-remote --tags origin | grep -q "refs/tags/$TAG_NAME$"; then
        echo -e "${RED}Error: Tag $TAG_NAME already exists on remote${NC}"
        exit 1
    fi
}

check_remote_tag

# Display version bump information
echo -e "${YELLOW}Bumping version:${NC}"
echo "  Current: $CURRENT_VERSION"
echo "  New:     $NEW_VERSION"
if [[ "$BUMP_TYPE" == "auto" ]]; then
    echo "  Bump:    auto (derived from Conventional Commits)"
else
    echo "  Bump:    $BUMP_TYPE"
fi
echo ""

# Dry-run mode: show preview and exit
if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY-RUN MODE]${NC}"
    echo ""
    echo "The following changes would be made:"
    echo "  1. commit-and-tag-version bumps package.json + VERSION + sd-frontend/package.json: $CURRENT_VERSION → $NEW_VERSION"
    echo "  2. Sync backend (Directory.Build.props) + OpenAPI spec versions to $NEW_VERSION"
    echo "  3. Create commit: 'chore: bump version to $NEW_VERSION'"
    echo "  4. Create tag: $TAG_NAME"
    echo "  5. Generate changelog entry for $TAG_NAME (git-cliff) and amend commit"
    echo "  6. Push to remote: origin/$(git rev-parse --abbrev-ref HEAD)"
    echo ""
    echo -e "${GREEN}✓${NC} Dry-run completed (no changes made)"
    exit 0
fi

# Interactive confirmation (unless auto-confirm flag set)
confirm_action() {
    if [[ "$AUTO_CONFIRM" == true ]]; then
        return 0
    fi

    echo "Ready to:"
    echo "  1. commit-and-tag-version bumps package.json + VERSION + sd-frontend/package.json: $CURRENT_VERSION → $NEW_VERSION"
    echo "  2. Sync backend (Directory.Build.props) + OpenAPI spec versions to $NEW_VERSION"
    echo "  3. Create commit: 'chore: bump version to $NEW_VERSION'"
    echo "  4. Create tag: $TAG_NAME"
    echo "  5. Generate changelog entry for $TAG_NAME (git-cliff) and amend commit"
    echo "  6. Push to remote: origin/$(git rev-parse --abbrev-ref HEAD)"
    echo ""
    read -p "Continue? [y/N] " -n 1 -r
    echo ""

    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Aborted by user${NC}"
        exit 0
    fi
}

confirm_action

# Rollback mechanism
rollback() {
    if [[ "$ROLLBACK_ACTIVE" == true ]]; then
        return  # Prevent recursive rollback
    fi

    ROLLBACK_ACTIVE=true
    echo ""
    echo -e "${RED}Error occurred - rolling back changes...${NC}"

    # Delete tag if created
    if [[ "$TAG_CREATED" == true ]]; then
        git tag -d "$TAG_NAME" >/dev/null 2>&1 || true
        echo -e "${YELLOW}✓${NC} Removed tag $TAG_NAME"
    fi

    # Reset commit if created (restores package.json + VERSION + CHANGELOG.md)
    if [[ "$COMMIT_CREATED" == true ]]; then
        git reset --hard HEAD~1 >/dev/null 2>&1 || true
        echo -e "${YELLOW}✓${NC} Reverted commit"
    fi

    echo -e "${RED}Rollback completed${NC}"
    exit 1
}

# Set trap for rollback on error
trap rollback EXIT

# 1. Run commit-and-tag-version to bump package.json + VERSION + sd-frontend/package.json on disk.
#    Changelog/commit/tag are skipped — we handle those ourselves below
#    so we can fold the git-cliff changelog into the bump commit.
echo -e "${YELLOW}Running commit-and-tag-version...${NC}"
pnpm exec commit-and-tag-version \
    $CATV_RELEASE_ARG \
    --skip.changelog --skip.commit --skip.tag
echo -e "${GREEN}✓${NC} Bumped package.json + VERSION + sd-frontend/package.json"

# 1b. Sync backend assembly version and OpenAPI spec version with the new release version.
BACKEND_PROPS="sd-backend/Directory.Build.props"
API_SPEC="docs/api/splitduoapi-v1.yaml"
sed -i -E "s|<Version>[0-9]+\.[0-9]+\.[0-9]+</Version>|<Version>$NEW_VERSION</Version>|" "$BACKEND_PROPS"
sed -i -E "s|^(  version: )[0-9]+\.[0-9]+\.[0-9]+$|\1$NEW_VERSION|" "$API_SPEC"
echo -e "${GREEN}✓${NC} Synced $BACKEND_PROPS + $API_SPEC to $NEW_VERSION"

# 2. Stage and commit the version bump.
git add package.json "$VERSION_FILE" sd-frontend/package.json sd-backend/Directory.Build.props docs/api/splitduoapi-v1.yaml
git commit -m "chore: bump version to $NEW_VERSION"
COMMIT_CREATED=true
echo -e "${GREEN}✓${NC} Committed version bump"

# 3. Generate changelog entry for this release and fold it into the bump commit.
#    Done BEFORE tagging so the tag points at the amended commit (with changelog).
#    git-cliff emits only the entry for the target tag via --tag, prepended to
#    CHANGELOG.md. The bump commit is then amended to include the changelog update.
if command -v git-cliff >/dev/null 2>&1 || [ -x "./node_modules/.bin/git-cliff" ]; then
    echo -e "${YELLOW}Generating changelog entry for $TAG_NAME...${NC}"
    if pnpm exec git-cliff --tag "$TAG_NAME" --prepend CHANGELOG.md -u; then
        git add CHANGELOG.md
        git commit --amend --no-edit
        echo -e "${GREEN}✓${NC} Updated CHANGELOG.md and amended bump commit"
    else
        echo -e "${YELLOW}Warning: git-cliff failed; continuing without changelog update${NC}"
    fi
else
    echo -e "${YELLOW}Warning: git-cliff not found; skipping changelog update${NC}"
    echo "         Install with: pnpm install (at repo root)"
fi

# 4. Create annotated tag (after amend, so it points at the commit with the changelog)
git tag -a "$TAG_NAME" -m "Release version $NEW_VERSION"
TAG_CREATED=true
echo -e "${GREEN}✓${NC} Created git tag $TAG_NAME"

# 5. Push commit to remote
echo -e "${YELLOW}Pushing commit to remote...${NC}"
git push
echo -e "${GREEN}✓${NC} Pushed commit to remote"

# 6. Push tag to remote
echo -e "${YELLOW}Pushing tag to remote...${NC}"
git push origin "$TAG_NAME"
echo -e "${GREEN}✓${NC} Pushed tag $TAG_NAME to remote"

# Clear trap on success
trap - EXIT

echo ""
echo -e "${GREEN}🎉 Version bumped successfully!${NC}"
echo -e "Version: ${GREEN}$NEW_VERSION${NC}"
echo -e "Tag: ${GREEN}$TAG_NAME${NC}"
echo ""
echo "Your GitLab CI/CD pipeline should now build and push Docker image: j1mm0/splitduo:$NEW_VERSION"