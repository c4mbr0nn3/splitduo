#!/bin/bash

# Bump version script following semantic versioning
# Usage: ./bump-version.sh [major|minor|patch] [-d|--dry-run] [-y|--yes] [-h|--help]
# Default: patch

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
Usage: $0 [major|minor|patch] [OPTIONS]

Bump version following semantic versioning.

Arguments:
  major|minor|patch    Version component to bump (default: patch)

Options:
  -d, --dry-run       Preview changes without making modifications
  -y, --yes           Skip confirmation prompt (auto-confirm)
  -h, --help          Display this help message

Examples:
  $0 patch            # Bump patch version (interactive)
  $0 minor -y         # Bump minor version (auto-confirm)
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

# Default to patch if not specified
BUMP_TYPE="${BUMP_TYPE:-patch}"

# Validate bump type
if [[ ! "$BUMP_TYPE" =~ ^(major|minor|patch)$ ]]; then
    echo -e "${RED}Error: Invalid bump type '$BUMP_TYPE'${NC}"
    echo "Usage: $0 [major|minor|patch]"
    exit 1
fi

# Check if VERSION file exists
VERSION_FILE="VERSION"
if [[ ! -f "$VERSION_FILE" ]]; then
    echo -e "${RED}Error: VERSION file not found${NC}"
    exit 1
fi

# Read current version
CURRENT_VERSION=$(cat "$VERSION_FILE" | tr -d '[:space:]')

# Validate version format
if [[ ! "$CURRENT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}Error: Invalid version format in VERSION file: $CURRENT_VERSION${NC}"
    echo "Expected format: X.Y.Z"
    exit 1
fi

# Parse version components
IFS='.' read -r -a version_parts <<< "$CURRENT_VERSION"
MAJOR="${version_parts[0]}"
MINOR="${version_parts[1]}"
PATCH="${version_parts[2]}"

# Calculate new version
case "$BUMP_TYPE" in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="$MAJOR.$MINOR.$PATCH"
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
echo "  Bump:    $BUMP_TYPE"
echo ""

# Dry-run mode: show preview and exit
if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY-RUN MODE]${NC}"
    echo ""
    echo "The following changes would be made:"
    echo "  1. Update VERSION file: $CURRENT_VERSION → $NEW_VERSION"
    echo "  2. Create commit: 'chore: bump version to $NEW_VERSION'"
    echo "  3. Create tag: $TAG_NAME"
    echo "  4. Generate changelog entry for $TAG_NAME (git-cliff) and amend commit"
    echo "  5. Push to remote: origin/$(git rev-parse --abbrev-ref HEAD)"
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
    echo "  1. Update VERSION file: $CURRENT_VERSION → $NEW_VERSION"
    echo "  2. Create commit: 'chore: bump version to $NEW_VERSION'"
    echo "  3. Create tag: $TAG_NAME"
    echo "  4. Generate changelog entry for $TAG_NAME (git-cliff) and amend commit"
    echo "  5. Push to remote: origin/$(git rev-parse --abbrev-ref HEAD)"
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

    # Reset commit if created
    if [[ "$COMMIT_CREATED" == true ]]; then
        git reset --hard HEAD~1 >/dev/null 2>&1 || true
        echo -e "${YELLOW}✓${NC} Reverted commit"
    fi

    # Restore VERSION file
    echo "$CURRENT_VERSION" > "$VERSION_FILE"
    echo -e "${YELLOW}✓${NC} Restored VERSION file to $CURRENT_VERSION"

    echo -e "${RED}Rollback completed${NC}"
    exit 1
}

# Set trap for rollback on error
trap rollback EXIT

# Update VERSION file
echo "$NEW_VERSION" > "$VERSION_FILE"
echo -e "${GREEN}✓${NC} Updated VERSION file"

# Commit the change
git add "$VERSION_FILE"
git commit -m "chore: bump version to $NEW_VERSION"
COMMIT_CREATED=true
echo -e "${GREEN}✓${NC} Committed VERSION file"

# Create annotated tag
git tag -a "$TAG_NAME" -m "Release version $NEW_VERSION"
TAG_CREATED=true
echo -e "${GREEN}✓${NC} Created git tag $TAG_NAME"

# Generate changelog entry for this release and fold it into the bump commit.
# git-cliff reads the new tag from the working tree and emits only the entry for
# it via --tag. The entry is prepended to CHANGELOG.md, then the bump commit is
# amended to include the changelog update.
if command -v git-cliff >/dev/null 2>&1 || [ -x "./node_modules/.bin/git-cliff" ]; then
    echo -e "${YELLOW}Generating changelog entry for $TAG_NAME...${NC}"
    if npx --no-install git-cliff --tag "$TAG_NAME" --prepend CHANGELOG.md -u; then
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

# Push commit to remote
echo -e "${YELLOW}Pushing commit to remote...${NC}"
git push
echo -e "${GREEN}✓${NC} Pushed commit to remote"

# Push tag to remote
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
