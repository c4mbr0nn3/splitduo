#!/usr/bin/env bash
set -e

# Script configuration
CONTAINER_NAME="splitduo-mailpit"
IMAGE_NAME="ghcr.io/axllent/mailpit:latest"
UI_PORT=8025
SMTP_PORT=1025

# Volume configuration
VOLUME_NAME="splitduo-mailpit-data"

# Check if container is already running
if podman container exists "${CONTAINER_NAME}"; then
    echo "Container ${CONTAINER_NAME} already exists. Stopping and removing..."
    podman stop "${CONTAINER_NAME}" || true
    podman rm "${CONTAINER_NAME}" || true
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
    echo "📁 Data volume: ${VOLUME_NAME}"
else
    echo "❌ Failed to start Mailpit container. Check the logs for details."
    exit 1
fi