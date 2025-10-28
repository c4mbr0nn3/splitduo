#!/bin/bash

# Bump version script following semantic versioning
# Usage: ./bump-version.sh [major|minor|patch]
# Default: patch

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Get bump type (default to patch)
BUMP_TYPE="${1:-patch}"

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

echo -e "${YELLOW}Bumping version:${NC}"
echo "  Current: $CURRENT_VERSION"
echo "  New:     $NEW_VERSION"
echo "  Bump:    $BUMP_TYPE"
echo ""

# Check for uncommitted changes
if [[ -n $(git status --porcelain | grep -v "^?? ") ]]; then
    echo -e "${RED}Error: You have uncommitted changes. Please commit or stash them first.${NC}"
    git status --short
    exit 1
fi

# Check if tag already exists
if git rev-parse "$TAG_NAME" >/dev/null 2>&1; then
    echo -e "${RED}Error: Tag $TAG_NAME already exists${NC}"
    exit 1
fi

# Update VERSION file
echo "$NEW_VERSION" > "$VERSION_FILE"
echo -e "${GREEN}✓${NC} Updated VERSION file"

# Commit the change
git add "$VERSION_FILE"
git commit -m "chore: bump version to $NEW_VERSION"
echo -e "${GREEN}✓${NC} Committed VERSION file"

# Create annotated tag
git tag -a "$TAG_NAME" -m "Release version $NEW_VERSION"
echo -e "${GREEN}✓${NC} Created git tag $TAG_NAME"

# Push commit to remote
echo -e "${YELLOW}Pushing commit to remote...${NC}"
git push
echo -e "${GREEN}✓${NC} Pushed commit to remote"

# Push tag to remote
echo -e "${YELLOW}Pushing tag to remote...${NC}"
git push origin "$TAG_NAME"
echo -e "${GREEN}✓${NC} Pushed tag $TAG_NAME to remote"

echo ""
echo -e "${GREEN}🎉 Version bumped successfully!${NC}"
echo -e "Version: ${GREEN}$NEW_VERSION${NC}"
echo -e "Tag: ${GREEN}$TAG_NAME${NC}"
echo ""
echo "Your GitLab CI/CD pipeline should now build and push Docker image: j1mm0/splitduo:$NEW_VERSION"
