#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DROP_VOLUME=false

usage() {
  echo "Usage: $0 [-d] [postgres|mailpit]"
  echo "  -d: Drop (remove and recreate) existing data volumes before starting"
  echo "  postgres|mailpit: Start only that service (default: both)"
  exit 1
}

while getopts "d" opt; do
  case ${opt} in
    d) DROP_VOLUME=true ;;
    \?) usage ;;
  esac
done
shift $((OPTIND - 1))

TARGET="${1:-all}"

FLAGS=""
[ "$DROP_VOLUME" = true ] && FLAGS="-d"

case "$TARGET" in
  postgres)
    bash "${SCRIPT_DIR}/postgresql/start.sh" $FLAGS
    ;;
  mailpit)
    bash "${SCRIPT_DIR}/mailpit/start.sh" $FLAGS
    ;;
  all)
    echo "=== Starting PostgreSQL ==="
    bash "${SCRIPT_DIR}/postgresql/start.sh" $FLAGS
    echo ""
    echo "=== Starting Mailpit ==="
    bash "${SCRIPT_DIR}/mailpit/start.sh" $FLAGS
    echo ""
    echo "✅ Dev services ready"
    echo "   PostgreSQL: localhost:5432"
    echo "   Mailpit SMTP: localhost:1025"
    echo "   Mailpit UI:   http://localhost:8025"
    ;;
  *)
    usage
    ;;
esac
