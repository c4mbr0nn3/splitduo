#!/usr/bin/env bash
set -euo pipefail

# --- Podman socket (rootless) ---
# Auto-detect the per-user socket path
SOCKET_PATH="${DOCKER_HOST:-unix:///run/user/$(id -u)/podman/podman.sock}"
export DOCKER_HOST="$SOCKET_PATH"

# Ryuk (Testcontainers resource reaper) needs privileged mode under rootless podman
export TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED=true

# In-container socket path override (must match DOCKER_HOST for Ryuk to mount it)
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE="${SOCKET_PATH#unix://}"

# --- SplitDuo test config ---
export ASPNETCORE_ENVIRONMENT=Development
export SD_JWT_SECRET_KEY="test-integration-secret-key-32-chars-min!!"
export SD_JWT_ISSUER="test"
export SD_JWT_AUDIENCE="test"
export SD_SEED_DEMO_DATA=false

RESULTS_DIR="$(pwd)/TestResults"
REPORT_DIR="${RESULTS_DIR}/coverage-report"

# Clean previous results so reportgenerator doesn't merge stale runs
rm -rf "${RESULTS_DIR}"
mkdir -p "${RESULTS_DIR}"

echo "DOCKER_HOST=$DOCKER_HOST"
echo "Collecting coverage..."

# Unit tests (no podman env needed, but harmless)
dotnet test SplitDuo.Tests.Unit/SplitDuo.Tests.Unit.csproj \
  --verbosity minimal \
  --collect:"XPlat Code Coverage" \
  --results-directory "${RESULTS_DIR}"

# Integration tests (podman env set above)
dotnet test SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj \
  --verbosity minimal \
  --collect:"XPlat Code Coverage" \
  --results-directory "${RESULTS_DIR}"

# Aggregate cobertura files into an HTML report
COBERTURA_FILES=$(find "${RESULTS_DIR}" -name "coverage.cobertura.xml" | paste -sd, -)
if [ -z "${COBERTURA_FILES}" ]; then
  echo "No coverage.cobertura.xml found under ${RESULTS_DIR}"
  exit 1
fi

if ! command -v reportgenerator >/dev/null 2>&1; then
  echo "reportgenerator not found. Install with:"
  echo "  dotnet tool install -g dotnet-reportgenerator-globaltool"
  echo ""
  echo "Cobertura files (raw XML):"
  echo "${COBERTURA_FILES}" | tr ',' '\n'
  exit 0
fi

echo "Generating HTML report..."
reportgenerator \
  -reports:"${COBERTURA_FILES}" \
  -targetdir:"${REPORT_DIR}" \
  -reporttypes:Html

echo ""
echo "Coverage report: ${REPORT_DIR}/index.html"