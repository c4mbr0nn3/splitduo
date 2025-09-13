#!/usr/bin/env bash
set -e

# Script configuration
IMAGE_NAME="splitduo"
TAG="test"
FULL_IMAGE_NAME="${IMAGE_NAME}:${TAG}"
FORCE_REBUILD=false
NO_CACHE=false

# Parse command line options
while getopts "fn" opt; do
  case ${opt} in
    f )
      FORCE_REBUILD=true
      ;;
    n )
      NO_CACHE=true
      ;;
    \? )
      echo "Usage: $0 [-f] [-n]"
      echo "  -f: Force rebuild (remove existing image before building)"
      echo "  -n: Build without cache (--no-cache)"
      exit 1
      ;;
  esac
done
shift $((OPTIND -1))

echo "🐳 Testing Docker build for SplitDuo application..."
echo "   Image: ${FULL_IMAGE_NAME}"

# Remove existing image if force rebuild is requested
if [ "$FORCE_REBUILD" = true ]; then
    echo "⚠️ Force rebuild requested. Removing existing image if it exists..."
    if podman image exists "${FULL_IMAGE_NAME}"; then
        podman rmi "${FULL_IMAGE_NAME}"
        if [ $? -eq 0 ]; then
            echo "✅ Existing image removed."
        else
            echo "❌ Failed to remove existing image."
            exit 1
        fi
    else
        echo "ℹ️ Image ${FULL_IMAGE_NAME} does not exist. No removal needed."
    fi
fi

# Build the image
echo "🔨 Building Docker image..."
BUILD_ARGS="--tag ${FULL_IMAGE_NAME} --rm"

if [ "$NO_CACHE" = true ]; then
    BUILD_ARGS="${BUILD_ARGS} --no-cache"
    echo "   Building without cache..."
fi

# Run the build command
podman build ${BUILD_ARGS} .

# Check if build was successful
if [ $? -eq 0 ]; then
    echo "✅ Docker build completed successfully!"
    echo "   Image: ${FULL_IMAGE_NAME}"

    # Show image details
    echo ""
    echo "📊 Image information:"
    podman images "${FULL_IMAGE_NAME}"

    # Get image size
    IMAGE_SIZE=$(podman inspect "${FULL_IMAGE_NAME}" --format "{{.Size}}" | numfmt --to=iec-i --suffix=B --format="%.1f")
    echo "   Size: ${IMAGE_SIZE}"

    echo ""
    echo "🎉 Build test completed successfully!"
    echo "   You can now run the container with:"
    echo "   podman run --rm -p 8080:8080 ${FULL_IMAGE_NAME}"
else
    echo "❌ Docker build failed. Check the output above for details."
    exit 1
fi