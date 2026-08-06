#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT=""
NO_VERIFY=false
TRIVY_SEVERITY="HIGH,CRITICAL"
TRIVY_FORMAT="table"

usage() {
  echo "Usage: $0 [-o OUTPUT_FILE] [-n] [-s SEVERITY] [-f FORMAT] [trivy|trufflehog]"
  echo "  -o: Write reports to OUTPUT_FILE (trivy appends .trivy, trufflehog appends .trufflehog)"
  echo "  -n: TruffleHog no-verification mode"
  echo "  -s: Trivy severity filter (default: HIGH,CRITICAL)"
  echo "  -f: Trivy output format (default: table)"
  echo "  trivy|trufflehog: Run only that scanner (default: both)"
  exit 1
}

while getopts "o:ns:f:" opt; do
  case ${opt} in
    o) OUTPUT="$OPTARG" ;;
    n) NO_VERIFY=true ;;
    s) TRIVY_SEVERITY="$OPTARG" ;;
    f) TRIVY_FORMAT="$OPTARG" ;;
    \?) usage ;;
  esac
done
shift $((OPTIND - 1))

TARGET="${1:-all}"

TRUFFLEHOG_FLAGS=""
[ "$NO_VERIFY" = true ] && TRUFFLEHOG_FLAGS="-n"

case "$TARGET" in
  trivy)
    bash "${SCRIPT_DIR}/trivy/scan.sh" -s "$TRIVY_SEVERITY" -f "$TRIVY_FORMAT" ${OUTPUT:+-o "$OUTPUT"}
    ;;
  trufflehog)
    bash "${SCRIPT_DIR}/trufflehog/scan.sh" $TRUFFLEHOG_FLAGS ${OUTPUT:+-o "$OUTPUT"}
    ;;
  all)
    echo "=== Running Trivy filesystem scan ==="
    bash "${SCRIPT_DIR}/trivy/scan.sh" -s "$TRIVY_SEVERITY" -f "$TRIVY_FORMAT" ${OUTPUT:+-o "$OUTPUT"}
    echo ""
    echo "=== Running TruffleHog secrets scan ==="
    bash "${SCRIPT_DIR}/trufflehog/scan.sh" $TRUFFLEHOG_FLAGS ${OUTPUT:+-o "$OUTPUT"}
    echo ""
    echo "✅ Security scans complete"
    ;;
  *)
    usage
    ;;
esac