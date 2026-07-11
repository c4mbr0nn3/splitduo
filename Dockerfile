# Multi-stage Dockerfile for SplitDuo application

# Stage 1: Build frontend (Nuxt.js)
FROM node:22-alpine AS frontend-build

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
WORKDIR /app/backend

# Copy solution and project files
COPY sd-backend/sd-backend.sln ./
COPY sd-backend/SplitDuo.Api/SplitDuo.Api.csproj ./SplitDuo.Api/
COPY sd-backend/SplitDuo.Core/SplitDuo.Core.csproj ./SplitDuo.Core/

# Restore dependencies
RUN dotnet restore

# Copy source code and build
COPY sd-backend/ ./
RUN dotnet publish SplitDuo.Api/SplitDuo.Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Copy backend application
COPY --from=backend-build /app/publish ./

# Copy frontend build output to wwwroot for static file serving
COPY --from=frontend-build /app/frontend/.output/public ./wwwroot

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

# Start the application
ENTRYPOINT ["dotnet", "SplitDuo.Api.dll"]
