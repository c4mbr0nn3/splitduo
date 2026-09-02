# Multi-stage Dockerfile for SplitDuo application

# Stage 1: Build frontend (Nuxt.js)
FROM --platform=$BUILDPLATFORM node:22-alpine AS frontend-build

# Include this stage's packages in the SBOM attestation
ARG BUILDKIT_SBOM_SCAN_STAGE=true

# Accept version as build argument
ARG APP_VERSION
ENV NUXT_PUBLIC_APP_VERSION=${APP_VERSION}

RUN npm install -g pnpm@latest-10

WORKDIR /app/frontend

# Copy frontend package files
COPY sd-frontend/package.json sd-frontend/pnpm-lock.yaml sd-frontend/pnpm-workspace.yaml sd-frontend/.npmrc ./
RUN pnpm install --frozen-lockfile

# Copy frontend source code and build
COPY sd-frontend/ ./
RUN pnpm generate

# Stage 2: Build backend (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build

# Include this stage's packages in the SBOM attestation
ARG BUILDKIT_SBOM_SCAN_STAGE=true

WORKDIR /app/backend

# Copy solution and project files
COPY sd-backend/sd-backend.sln ./
COPY sd-backend/SplitDuo.Api/SplitDuo.Api.csproj ./SplitDuo.Api/
COPY sd-backend/SplitDuo.Core/SplitDuo.Core.csproj ./SplitDuo.Core/

# Restore dependencies (Api project only — test projects aren't published)
RUN dotnet restore SplitDuo.Api/SplitDuo.Api.csproj

# Copy source code and build
COPY sd-backend/ ./
RUN dotnet publish SplitDuo.Api/SplitDuo.Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# OCI image annotations (https://github.com/opencontainers/image-spec/blob/main/annotations.md)
ARG APP_VERSION
ARG GIT_REVISION
ARG BUILD_DATE
LABEL org.opencontainers.image.title="SplitDuo" \
      org.opencontainers.image.description="Expense splitting app for small groups — couples, housemates, travel companions, or anyone sharing costs" \
      org.opencontainers.image.authors="Francesco Zorzi" \
      org.opencontainers.image.documentation="https://gitlab.com/j1mm0/splitduo#readme" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_REVISION}" \
      org.opencontainers.image.created="${BUILD_DATE}" \
      org.opencontainers.image.source="https://gitlab.com/j1mm0/splitduo" \
      org.opencontainers.image.url="https://gitlab.com/j1mm0/splitduo" \
      org.opencontainers.image.licenses="MIT"

# Copy backend application
COPY --from=backend-build /app/publish ./

# Copy frontend build output to wwwroot for static file serving
COPY --from=frontend-build /app/frontend/.output/public ./wwwroot

# Install ICU for globalization support (Alpine ships in invariant mode by default;
# the app uses request localization with en/it cultures and IStringLocalizer)
RUN apk add --no-cache icu-libs icu-data-full

# Create a non-root user
RUN addgroup -g 1000 appuser && \
  adduser -u 1000 -G appuser -s /bin/sh -D appuser && \
  chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# Override Alpine image's default invariant mode (icu-data-full installed above)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Start the application
ENTRYPOINT ["dotnet", "SplitDuo.Api.dll"]
