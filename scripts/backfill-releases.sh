#!/bin/bash

# Backfill GitLab Releases for existing tags.
#
# Loops over git tags matching v* and creates a GitLab Release for each via the
# Releases API, using the matching CHANGELOG.md section as release notes.
# Idempotent: skips releases that already exist (HTTP 409 / 422), so it can be
# rerun safely.
#
# Requires a GitLab personal/project access token with `api` scope on the
# j1mm0/splitduo project. Export it as GITLAB_TOKEN before running.
#
# Usage: ./scripts/backfill-releases.sh [OPTIONS] [TAG...]
#
# Arguments:
#   TAG...               Specific tags to backfill (default: all v* tags, oldest first)
#
# Options:
#   -d, --dry-run        Preview which releases would be created, make no API calls
#   -n, --no-asset-link  Omit the Docker Hub asset link from each release
#   -h, --help           Display this help message
#
# Environment:
#   GITLAB_TOKEN         (required) GitLab access token with `api` scope
#   GITLAB_PROJECT_ID    (optional) project ID or URL-encoded path (default: j1mm0%2Fsplitduo)
#   GITLAB_API_URL       (optional) GitLab base URL (default: https://gitlab.com)
#
# Examples:
#   GITLAB_TOKEN=xxx ./scripts/backfill-releases.sh
#   GITLAB_TOKEN=xxx ./scripts/backfill-releases.sh v1.7.0 v1.7.1
#   GITLAB_TOKEN=xxx ./scripts/backfill-releases.sh --dry-run

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

DRY_RUN=false
NO_ASSET_LINK=false
EXPLICIT_TAGS=()

# Print usage information
print_usage() {
    cat << EOF
Usage: $0 [OPTIONS] [TAG...]

Backfill GitLab Releases for existing tags using CHANGELOG.md as release notes.

Arguments:
  TAG...               Specific tags to backfill (default: all v* tags, oldest first)

Options:
  -d, --dry-run        Preview which releases would be created, make no API calls
  -n, --no-asset-link  Omit the Docker Hub asset link from each release
  -h, --help           Display this help message

Environment:
  GITLAB_TOKEN         (required) GitLab access token with \`api\` scope
  GITLAB_PROJECT_ID    (optional) project ID or URL-encoded path (default: j1mm0%2Fsplitduo)
  GITLAB_API_URL       (optional) GitLab base URL (default: https://gitlab.com)

Examples:
  GITLAB_TOKEN=xxx $0
  GITLAB_TOKEN=xxx $0 v1.7.0 v1.7.1
  GITLAB_TOKEN=xxx $0 --dry-run
EOF
    exit 0
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -n|--no-asset-link)
            NO_ASSET_LINK=true
            shift
            ;;
        -h|--help)
            print_usage
            ;;
        --*)
            echo -e "${RED}Error: Unknown option '$1'${NC}"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
        *)
            EXPLICIT_TAGS+=("$1")
            shift
            ;;
    esac
done

# Resolve config
GITLAB_API_URL="${GITLAB_API_URL:-https://gitlab.com}"
GITLAB_PROJECT_ID="${GITLAB_PROJECT_ID:-j1mm0%2Fsplitduo}"
API_BASE="$GITLAB_API_URL/api/v4/projects/$GITLAB_PROJECT_ID/releases"

# Validate token (only when not dry-run)
if [[ "$DRY_RUN" == false ]]; then
    if [[ -z "${GITLAB_TOKEN:-}" ]]; then
        echo -e "${RED}Error: GITLAB_TOKEN is required (api scope). Export it before running.${NC}"
        echo "  GITLAB_TOKEN=xxx $0"
        exit 1
    fi
fi

# Check CHANGELOG.md exists
if [[ ! -f "CHANGELOG.md" ]]; then
    echo -e "${RED}Error: CHANGELOG.md not found. Run from the repo root.${NC}"
    exit 1
fi

# Build the tag list: explicit tags if given, otherwise all v* tags oldest-first.
if [[ ${#EXPLICIT_TAGS[@]} -gt 0 ]]; then
    TAGS=("${EXPLICIT_TAGS[@]}")
else
    # git tag --list 'v*' returns creation order; sort by version ascending.
    mapfile -t TAGS < <(git tag --list 'v*' --sort=v:refname)
fi

if [[ ${#TAGS[@]} -eq 0 ]]; then
    echo -e "${YELLOW}No v* tags found. Nothing to backfill.${NC}"
    exit 0
fi

echo -e "${YELLOW}Backfilling GitLab Releases for ${#TAGS[@]} tag(s)${NC}"
if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY-RUN MODE]${NC} No API calls will be made."
fi
echo ""

# Extract the changelog section for a given version (without the "v" prefix).
# Mirrors the awk logic in ci/release.yml.
extract_changelog() {
    local version="$1"
    awk -v ver="$version" '
        $0 ~ "^## \\[" ver "\\]" { found=1; next }
        found && /^## \[/ { exit }
        found { print }
    ' CHANGELOG.md
}

# Create a release for one tag. Returns 0 on success, 1 on skip, 2 on error.
create_release() {
    local tag="$1"
    local version="${tag#v}"
    local description
    description=$(extract_changelog "$version")

    if [[ -z "$description" ]]; then
        echo -e "${YELLOW}Warning: no changelog section for $version; using fallback${NC}"
        description="Release $tag"
    fi

    if [[ "$DRY_RUN" == true ]]; then
        echo -e "${GREEN}[dry-run]${NC} Would create release $tag"
        echo "         notes: $(echo "$description" | head -1 | sed 's/^#* *//')..."
        return 0
    fi

    # Build JSON payload. description is multi-line markdown; use jq for safe
    # escaping. -n (null input) is required because the filters are constant
    # literals — without it jq blocks waiting for stdin.
    local payload
    if command -v jq >/dev/null 2>&1; then
        local links_json="[]"
        if [[ "$NO_ASSET_LINK" == false ]]; then
            links_json=$(jq -c -n --arg url "https://hub.docker.com/r/j1mm0/splitduo/tags?name=$version" \
                '[{"name":"Docker image","url":$url,"link_type":"image"}]')
        fi
        payload=$(jq -c -n \
            --arg name "Release $tag" \
            --arg tag_name "$tag" \
            --arg description "$description" \
            --argjson links "$links_json" \
            '{name:$name, tag_name:$tag_name, description:$description, assets:{links:$links}}')
    else
        echo -e "${RED}Error: jq is required to build the release payload safely.${NC}"
        echo "Install jq (e.g. \`sudo dnf install jq\`) and rerun."
        return 2
    fi

    local http_code body
    echo -e "${YELLOW}→${NC} POST $API_BASE for $tag ..."
    body=$(curl -sS --connect-timeout 10 --max-time 60 -w "\n%{http_code}" \
        --header "PRIVATE-TOKEN: $GITLAB_TOKEN" \
        --header "Content-Type: application/json" \
        --data "$payload" \
        --request POST "$API_BASE") || {
        echo -e "${RED}✗${NC} Failed $tag (curl error: connection timed out or failed)"
        return 2
    }
    http_code=$(echo "$body" | tail -n1)
    body=$(echo "$body" | sed '$d')

    case "$http_code" in
        201)
            echo -e "${GREEN}✓${NC} Created release $tag"
            return 0
            ;;
        409|422)
            echo -e "${YELLOW}⊘${NC} Skipped $tag (release already exists)"
            return 1
            ;;
        *)
            echo -e "${RED}✗${NC} Failed $tag (HTTP $http_code)"
            echo "$body" | head -5 | sed 's/^/          /'
            return 2
            ;;
    esac
}

# Main loop
created=0
skipped=0
failed=0
for tag in "${TAGS[@]}"; do
    if [[ ! "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+.*$ ]]; then
        echo -e "${YELLOW}Skipping $tag (not a v*.*.* tag)${NC}"
        continue
    fi
    if create_release "$tag"; then
        created=$((created + 1))
    else
        rc=$?
        if [[ $rc -eq 1 ]]; then
            skipped=$((skipped + 1))
        else
            failed=$((failed + 1))
        fi
    fi
done

echo ""
echo -e "${YELLOW}Summary:${NC} created=$created skipped=$skipped failed=$failed"
if [[ $failed -gt 0 ]]; then
    exit 1
fi
exit 0