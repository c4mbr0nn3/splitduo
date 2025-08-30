#!/usr/bin/env bash
set -e

# Script configuration
CONTAINER_NAME="splitduo-mailpit"
IMAGE_NAME="ghcr.io/axllent/mailpit:latest"
UI_PORT=8025
SMTP_PORT=1025
VOLUME_NAME="splitduo-mailpit-data"
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

# Check if container is already running
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

# Create volume if it doesn't exist
echo "Creating volume ${VOLUME_NAME} if it doesn't exist..."
podman volume create "${VOLUME_NAME}" || true

# Pull the latest image
echo "Pulling latest ${IMAGE_NAME} image..."
podman pull "${IMAGE_NAME}"

# Start the container
echo "Starting Mailpit container..."
podman run -d \
--name=${CONTAINER_NAME} \
--restart unless-stopped \
-v "${VOLUME_NAME}:/data" \
-e MP_DATABASE=/data/mailpit.db \
-e MP_SMTP_AUTH_ACCEPT_ANY=true \
-e MP_SMTP_AUTH_ALLOW_INSECURE=true \
-p "${UI_PORT}":8025 \
-p "${SMTP_PORT}":1025 \
"${IMAGE_NAME}"


# Check if container started successfully
if [ $? -eq 0 ]; then
    echo "✅ Mailpit started successfully!"
    echo "📧 SMTP server running on port ${SMTP_PORT}"
    echo "🌐 Web UI available at http://localhost:${UI_PORT}"
    echo "📁 Data Volume: ${VOLUME_NAME}"
else
    echo "❌ Failed to start Mailpit container. Check the logs for details."
    exit 1
fi