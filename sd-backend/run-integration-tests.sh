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

echo "DOCKER_HOST=$DOCKER_HOST"
echo "Running integration tests..."
exec dotnet test SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj \
  --verbosity normal \
  "$@"
