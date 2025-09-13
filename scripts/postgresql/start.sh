#!/usr/bin/env bash
set -e

CONTAINER_NAME="splitduo-db"
IMAGE_NAME="docker.io/postgres:17-alpine"
DB_NAME="splitduo"
DB_USER="splitduo"
DB_PASSWORD="splitduo"
HOST_PORT=5432
CONTAINER_PORT=5432
VOLUME_NAME="splitduo-db-data"
DROP_VOLUME=false

while getopts "d" opt; do
  case ${opt} in
    d )
      DROP_VOLUME=true
      ;;
    \? )
      echo "Usage: $0 [-d]"
      echo "  -d: Drop (remove and recreate) the existing data volume before starting."
      exit 1
      ;;
  esac
done
shift $((OPTIND -1))

# Stop and remove the container first if it exists
if podman container exists "${CONTAINER_NAME}"; then
    echo "Container ${CONTAINER_NAME} already exists. Stopping and removing..."
    podman stop "${CONTAINER_NAME}" || true
    podman rm "${CONTAINER_NAME}" || true
fi

# Now, remove the volume if the -d flag was passed
if [ "$DROP_VOLUME" = true ]; then
    echo "⚠️ Option -d detected. Attempting to remove existing volume: ${VOLUME_NAME}"
    if podman volume exists "${VOLUME_NAME}"; then
        # Force removal in case it's stuck, though removing the container first should prevent this
        podman volume rm --force "${VOLUME_NAME}"
        if [ $? -ne 0 ]; then
            echo "❌ Failed to remove volume: ${VOLUME_NAME}"
            exit 1
        fi
        echo "✅ Volume removed."
    else
        echo "ℹ️ Volume ${VOLUME_NAME} does not exist. No removal needed."
    fi
fi

echo "Pulling latest ${IMAGE_NAME} image..."
podman pull "${IMAGE_NAME}"

echo "Starting PostgreSQL container..."
podman run -dt \
  --name=${CONTAINER_NAME} \
  --restart unless-stopped \
  -p "${HOST_PORT}":"${CONTAINER_PORT}" \
  -v "${VOLUME_NAME}:/var/lib/postgresql/data" \
  -e POSTGRES_USER=${DB_USER} \
  -e POSTGRES_PASSWORD=${DB_PASSWORD} \
  -e POSTGRES_DB=${DB_NAME} \
  "${IMAGE_NAME}"

if [ $? -eq 0 ]; then
    echo "✅ PostgreSQL started successfully!"
    echo "   Host Port: ${HOST_PORT}"
    echo "   DB Name: ${DB_NAME}"
    echo "   DB User: ${DB_USER}"
    echo "   Data Volume: ${VOLUME_NAME}"
else
    echo "❌ Failed to start PostgreSQL container. Check the logs for details."
    exit 1
fi
