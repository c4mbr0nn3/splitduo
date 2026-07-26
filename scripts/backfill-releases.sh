#!/bin/bash

# Backfill GitLab and/or GitHub Releases for existing tags.
#
# Loops over git tags matching v* and creates a Release for each via the
# Releases API, using the matching CHANGELOG.md section as release notes.
# Idempotent: skips releases that already exist (HTTP 409/422), so it can be
# rerun safely.
#
# By default targets GitLab only. Pass --github (or --gitlab --github) to
# also create GitHub releases on the mirrored repo c4mbr0nn3/splitduo.
#
# Requires tokens with appropriate scope, exported before running:
#   GITLAB_TOKEN — GitLab access token with `api` scope (Developer role)
#   GITHUB_TOKEN — GitHub PAT with `repo` scope, or fine-grained token with
#                  `Contents: read` + `Releases: write` on c4mbr0nn3/splitduo
#                  (only required when --github is passed)
#
# Usage: ./scripts/backfill-releases.sh [OPTIONS] [TAG...]
#
# Arguments:
#   TAG...               Specific tags to backfill (default: all v* tags, oldest first)
#
# Options:
#   --gitlab             Target GitLab releases (default; can combine with --github)
#   --github             Target GitHub releases on the mirrored repo
#   -d, --dry-run        Preview which releases would be created, make no API calls
#   -n, --no-asset-link  Omit the Docker Hub asset link from each release
#   -h, --help           Display this help message
#
# Environment:
#   GITLAB_TOKEN         (required for --gitlab) GitLab access token with `api` scope
#   GITHUB_TOKEN         (required for --github) GitHub token with release write scope
#   GITLAB_PROJECT_ID    (optional) project ID or URL-encoded path (default: j1mm0%2Fsplitduo)
#   GITLAB_API_URL       (optional) GitLab base URL (default: https://gitlab.com)
#   GITHUB_REPO          (optional) GitHub repo (default: c4mbr0nn3/splitduo)
#   GITHUB_API_URL       (optional) GitHub API base URL (default: https://api.github.com)
#
# Examples:
#   GITLAB_TOKEN=xxx ./scripts/backfill-releases.sh
#   GITLAB_TOKEN=xxx GITHUB_TOKEN=yyy ./scripts/backfill-releases.sh --gitlab --github
#   GITHUB_TOKEN=yyy ./scripts/backfill-releases.sh --github v1.7.0 v1.7.1
#   GITLAB_TOKEN=xxx ./scripts/backfill-releases.sh --dry-run

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

DRY_RUN=false
NO_ASSET_LINK=false
TARGET_GITLAB=false
TARGET_GITHUB=false
EXPLICIT_TAGS=()

# Print usage information
print_usage() {
    cat << EOF
Usage: $0 [OPTIONS] [TAG...]

Backfill GitLab and/or GitHub Releases for existing tags using CHANGELOG.md.

Arguments:
  TAG...               Specific tags to backfill (default: all v* tags, oldest first)

Options:
  --gitlab             Target GitLab releases (default; can combine with --github)
  --github             Target GitHub releases on the mirrored repo
  -d, --dry-run        Preview which releases would be created, make no API calls
  -n, --no-asset-link  Omit the Docker Hub asset link from each release
  -h, --help           Display this help message

Environment:
  GITLAB_TOKEN         (required for --gitlab) GitLab access token with \`api\` scope
  GITHUB_TOKEN         (required for --github) GitHub token with release write scope
  GITLAB_PROJECT_ID    (optional) project ID or URL-encoded path (default: j1mm0%2Fsplitduo)
  GITLAB_API_URL       (optional) GitLab base URL (default: https://gitlab.com)
  GITHUB_REPO          (optional) GitHub repo (default: c4mbr0nn3/splitduo)
  GITHUB_API_URL       (optional) GitHub API base URL (default: https://api.github.com)

Examples:
  GITLAB_TOKEN=xxx $0
  GITLAB_TOKEN=xxx GITHUB_TOKEN=yyy $0 --gitlab --github
  GITHUB_TOKEN=yyy $0 --github v1.7.0 v1.7.1
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
        --gitlab)
            TARGET_GITLAB=true
            shift
            ;;
        --github)
            TARGET_GITHUB=true
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

# Default to GitLab if no target specified
if [[ "$TARGET_GITLAB" == false && "$TARGET_GITHUB" == false ]]; then
    TARGET_GITLAB=true
fi

# Resolve config
GITLAB_API_URL="${GITLAB_API_URL:-https://gitlab.com}"
GITLAB_PROJECT_ID="${GITLAB_PROJECT_ID:-j1mm0%2Fsplitduo}"
GITLAB_API_BASE="$GITLAB_API_URL/api/v4/projects/$GITLAB_PROJECT_ID/releases"
GITHUB_API_URL="${GITHUB_API_URL:-https://api.github.com}"
GITHUB_REPO="${GITHUB_REPO:-c4mbr0nn3/splitduo}"
GITHUB_API_BASE="$GITHUB_API_URL/repos/$GITHUB_REPO/releases"

# Validate tokens (only when not dry-run)
if [[ "$DRY_RUN" == false ]]; then
    if [[ "$TARGET_GITLAB" == true && -z "${GITLAB_TOKEN:-}" ]]; then
        echo -e "${RED}Error: GITLAB_TOKEN is required (api scope). Export it before running.${NC}"
        echo "  GITLAB_TOKEN=xxx $0"
        exit 1
    fi
    if [[ "$TARGET_GITHUB" == true && -z "${GITHUB_TOKEN:-}" ]]; then
        echo -e "${RED}Error: GITHUB_TOKEN is required (repo scope). Export it before running.${NC}"
        echo "  GITHUB_TOKEN=yyy $0 --github"
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

TARGETS=""
[[ "$TARGET_GITLAB" == true ]] && TARGETS="$TARGETS gitlab"
[[ "$TARGET_GITHUB" == true ]] && TARGETS="$TARGETS github"
echo -e "${YELLOW}Backfilling${TARGETS} releases for ${#TAGS[@]} tag(s)${NC}"
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

# Create a GitLab release for one tag. Returns 0/1/2 (created/skipped/failed).
create_gitlab_release() {
    local tag="$1"
    local version="${tag#v}"
    local description
    description=$(extract_changelog "$version")

    if [[ -z "$description" ]]; then
        echo -e "${YELLOW}Warning: no changelog section for $version; using fallback${NC}"
        description="Release $tag"
    fi

    if [[ "$DRY_RUN" == true ]]; then
        echo -e "${GREEN}[dry-run/gitlab]${NC} Would create release $tag"
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
        echo -e "${RED}Error: jq is required to build the GitLab payload.${NC}"
        echo "Install jq (e.g. \`sudo dnf install jq\`) and rerun."
        return 2
    fi

    local http_code body
    echo -e "${YELLOW}→${NC} [gitlab] POST for $tag ..."
    body=$(curl -sS --connect-timeout 10 --max-time 60 -w "\n%{http_code}" \
        --header "PRIVATE-TOKEN: $GITLAB_TOKEN" \
        --header "Content-Type: application/json" \
        --data "$payload" \
        --request POST "$GITLAB_API_BASE") || {
        echo -e "${RED}✗${NC} [gitlab] Failed $tag (curl error)"
        return 2
    }
    http_code=$(echo "$body" | tail -n1)
    body=$(echo "$body" | sed '$d')

    case "$http_code" in
        201)
            echo -e "${GREEN}✓${NC} [gitlab] Created release $tag"
            return 0
            ;;
        409|422)
            echo -e "${YELLOW}⊘${NC} [gitlab] Skipped $tag (already exists)"
            return 1
            ;;
        *)
            echo -e "${RED}✗${NC} [gitlab] Failed $tag (HTTP $http_code)"
            echo "$body" | head -5 | sed 's/^/          /'
            return 2
            ;;
    esac
}

# Create a GitHub release for one tag. Returns 0/1/2 (created/skipped/failed).
create_github_release() {
    local tag="$1"
    local version="${tag#v}"
    local description
    description=$(extract_changelog "$version")

    if [[ -z "$description" ]]; then
        echo -e "${YELLOW}Warning: no changelog section for $version; using fallback${NC}"
        description="Release $tag"
    fi

    if [[ "$DRY_RUN" == true ]]; then
        echo -e "${GREEN}[dry-run/github]${NC} Would create release $tag"
        return 0
    fi

    # Build JSON payload with python3 (json.dumps escapes newlines correctly;
    # capturing jq output in a shell variable strips backslash escaping, so
    # write to a file and use curl --data @file).
    local payload_file
    payload_file=$(mktemp)
    if command -v python3 >/dev/null 2>&1; then
        printf '%s' "$description" > /tmp/opencode/sd_notes.md
        python3 -c "
import json
notes = open('/tmp/opencode/sd_notes.md').read()
print(json.dumps({
    'tag_name': '$tag',
    'name': 'Release $tag',
    'body': notes,
    'target_commitish': 'main'
}))
" > "$payload_file"
    else
        echo -e "${RED}Error: python3 is required to build the GitHub payload.${NC}"
        rm -f "$payload_file" /tmp/opencode/sd_notes.md
        return 2
    fi

    local http_code body
    echo -e "${YELLOW}→${NC} [github] POST for $tag ..."
    body=$(curl -sS --connect-timeout 10 --max-time 60 -w "\n%{http_code}" \
        --header "Authorization: Bearer $GITHUB_TOKEN" \
        --header "Accept: application/vnd.github+json" \
        --header "X-GitHub-Api-Version: 2022-11-28" \
        --data @"$payload_file" \
        --request POST "$GITHUB_API_BASE") || {
        echo -e "${RED}✗${NC} [github] Failed $tag (curl error)"
        rm -f "$payload_file" /tmp/opencode/sd_notes.md
        return 2
    }
    rm -f "$payload_file" /tmp/opencode/sd_notes.md
    http_code=$(echo "$body" | tail -n1)
    body=$(echo "$body" | sed '$d')

    case "$http_code" in
        201)
            echo -e "${GREEN}✓${NC} [github] Created release $tag"
            return 0
            ;;
        422)
            echo -e "${YELLOW}⊘${NC} [github] Skipped $tag (already exists)"
            return 1
            ;;
        *)
            echo -e "${RED}✗${NC} [github] Failed $tag (HTTP $http_code)"
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
    rc=0
    if [[ "$TARGET_GITLAB" == true ]]; then
        if create_gitlab_release "$tag"; then
            created=$((created + 1))
        else
            rc=$?
            if [[ $rc -eq 1 ]]; then skipped=$((skipped + 1)); else failed=$((failed + 1)); fi
        fi
    fi
    if [[ "$TARGET_GITHUB" == true ]]; then
        if create_github_release "$tag"; then
            created=$((created + 1))
        else
            rc=$?
            if [[ $rc -eq 1 ]]; then skipped=$((skipped + 1)); else failed=$((failed + 1)); fi
        fi
    fi
done

echo ""
echo -e "${YELLOW}Summary:${NC} created=$created skipped=$skipped failed=$failed"
if [[ $failed -gt 0 ]]; then
    exit 1
fi
exit 0