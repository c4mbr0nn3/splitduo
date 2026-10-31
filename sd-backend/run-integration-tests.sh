#!/usr/bin/env bash
set -euo pipefail

# --- Preflight: Testcontainers needs a Docker-compatible engine ---
# Fail here, in one line, instead of after a slow restore/build when there is
# no engine at all. Check for both engines every time: this repo runs on
# machines that have either Docker or (more commonly here) rootless Podman,
# and the two need different socket wiring.
have_docker=0
have_podman=0
command -v docker >/dev/null 2>&1 && have_docker=1
command -v podman >/dev/null 2>&1 && have_podman=1

if [ "$have_docker" -eq 0 ] && [ "$have_podman" -eq 0 ]; then
    echo "error: no container engine found — integration tests need Docker or Podman." >&2
    echo "       Install Docker, or Podman plus a running user socket:" >&2
    echo "         systemctl --user enable --now podman.socket" >&2
    echo "       Then re-run: ./run-integration-tests.sh" >&2
    exit 1
fi

# --- Podman socket (rootless) ---
if [ "$have_podman" -eq 1 ] && [ "$have_docker" -eq 0 ]; then
    if [ -z "${DOCKER_HOST:-}" ]; then
        # Auto-detect the per-user socket path
        SOCKET_PATH="unix:///run/user/$(id -u)/podman/podman.sock"
        export DOCKER_HOST="$SOCKET_PATH"
    else
        SOCKET_PATH="$DOCKER_HOST"
    fi

    # The socket is a separate systemd unit; without it Testcontainers fails
    # with an opaque connection error, so name the fix up front.
    if [ ! -S "${SOCKET_PATH#unix://}" ]; then
        echo "error: Podman socket ${SOCKET_PATH#unix://} is not running." >&2
        echo "       Start it with: systemctl --user enable --now podman.socket" >&2
        exit 1
    fi

    # Ryuk (Testcontainers resource reaper) needs privileged mode under rootless podman
    export TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED=true

    # In-container socket path override (must match DOCKER_HOST for Ryuk to mount it)
    export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE="${SOCKET_PATH#unix://}"
elif [ "$have_docker" -eq 1 ]; then
    # Docker: keep whatever DOCKER_HOST the user/CI set (default unix socket).
    if ! docker info >/dev/null 2>&1; then
        echo "error: the Docker daemon is not reachable (is it running, and is your user in the 'docker' group?)." >&2
        exit 1
    fi
fi

# --- SplitDuo test config ---
export ASPNETCORE_ENVIRONMENT=Development
export SD_JWT_SECRET_KEY="test-integration-secret-key-32-chars-min!!"
export SD_JWT_ISSUER="test"
export SD_JWT_AUDIENCE="test"
export SD_SEED_DEMO_DATA=false

echo "Engine: podman=${have_podman} docker=${have_docker} DOCKER_HOST=${DOCKER_HOST:-<default>}"
echo "Running integration tests..."
exec dotnet test SplitDuo.Tests.Integration/SplitDuo.Tests.Integration.csproj \
  --verbosity normal \
  "$@"
