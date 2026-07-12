#!/usr/bin/env bash
set -e

IMAGE_NAME="docker.io/aquasec/trivy:latest"
SEVERITY="HIGH,CRITICAL"
FORMAT="table"
OUTPUT=""
REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DOCKER_CMD="$(command -v podman || command -v docker)"

while getopts "s:f:o:" opt; do
  case ${opt} in
    s) SEVERITY="$OPTARG" ;;
    f) FORMAT="$OPTARG" ;;
    o) OUTPUT="$OPTARG" ;;
    \?) echo "Usage: $0 [-s SEVERITY] [-f FORMAT] [-o OUTPUT_FILE]"; exit 1 ;;
  esac
done

"$DOCKER_CMD" image inspect "$IMAGE_NAME" >/dev/null 2>&1 || "$DOCKER_CMD" pull "$IMAGE_NAME"

TRIVY_ARGS=(fs /repo --severity "${SEVERITY}" --format "${FORMAT}" --skip-dirs .git --exit-code 1)
[ -n "$OUTPUT" ] && TRIVY_ARGS+=(--output "/output/$(basename "$OUTPUT")")

echo "Running Trivy filesystem scan..."

if [ -n "$OUTPUT" ]; then
  "$DOCKER_CMD" run --rm \
    -v "${REPO_ROOT}:/repo:ro" \
    -v "$(dirname "$(realpath "$OUTPUT")"):/output" \
    "${IMAGE_NAME}" "${TRIVY_ARGS[@]}"
  echo "Report written to ${OUTPUT}"
else
  "$DOCKER_CMD" run --rm \
    -v "${REPO_ROOT}:/repo:ro" \
    "${IMAGE_NAME}" "${TRIVY_ARGS[@]}"
fi