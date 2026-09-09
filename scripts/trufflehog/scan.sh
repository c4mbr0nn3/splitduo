#!/usr/bin/env bash
set -e

IMAGE_NAME="trufflesecurity/trufflehog:3.97.4@sha256:562bc231afa9de3d04de44cfe624252b08207de1fc3cebc5e7ed92bed7f279e4"
OUTPUT=""
NO_VERIFY=false
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOCKER_CMD="$(command -v podman || command -v docker)"

while getopts "o:n" opt; do
  case ${opt} in
    o) OUTPUT="$OPTARG" ;;
    n) NO_VERIFY=true ;;
    \?) echo "Usage: $0 [-o OUTPUT_FILE] [-n (no-verify)]"; exit 1 ;;
  esac
done

"$DOCKER_CMD" image inspect "$IMAGE_NAME" >/dev/null 2>&1 || "$DOCKER_CMD" pull "$IMAGE_NAME"

TRUFFLEHOG_ARGS=(filesystem /repo --fail)
[ "$NO_VERIFY" = true ] && TRUFFLEHOG_ARGS+=(--no-verification) || TRUFFLEHOG_ARGS+=(--only-verified)
[ -n "$OUTPUT" ] && TRUFFLEHOG_ARGS+=(--json)

echo "Running TruffleHog secrets scan..."

if [ -n "$OUTPUT" ]; then
  "$DOCKER_CMD" run --rm \
    -v "${REPO_ROOT}:/repo:ro,z" \
    "${IMAGE_NAME}" "${TRUFFLEHOG_ARGS[@]}" > "$OUTPUT"
  echo "Report written to ${OUTPUT}"
else
  "$DOCKER_CMD" run --rm \
    -v "${REPO_ROOT}:/repo:ro,z" \
    "${IMAGE_NAME}" "${TRUFFLEHOG_ARGS[@]}"
fi