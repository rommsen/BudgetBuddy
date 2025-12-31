# Docker Build

> Multi-stage Docker builds for F# full-stack apps.

## Overview

Use multi-stage builds to create small production images. Build stage includes SDK and Node.js, runtime stage only includes ASP.NET Core runtime.

## When to Use This

- Building production images
- Optimizing image size
- Separating build and runtime dependencies

## Patterns

### Multi-Stage Dockerfile

```dockerfile
# ============================================
# Stage 1: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Install Node.js for Vite/Fable
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs

WORKDIR /app
COPY . .

# Restore dependencies
WORKDIR /app/src/Shared
RUN dotnet restore

WORKDIR /app/src/Server
RUN dotnet restore

WORKDIR /app/src/Client
RUN dotnet restore

# Install npm dependencies
WORKDIR /app
RUN npm install

# Build (order matters!)
WORKDIR /app/src/Shared
RUN dotnet build -c Release

WORKDIR /app
RUN npm run build

WORKDIR /app/src/Server
RUN dotnet publish -c Release -o /app/publish

# ============================================
# Stage 2: Runtime
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

# Copy server binaries
COPY --from=build /app/publish .

# Copy client static files
COPY --from=build /app/dist/public ./dist/public

# Create data directory
RUN mkdir -p /app/data

EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "Server.dll"]
```

### Build Script

```bash
#!/bin/bash
set -e

APP_NAME="my-app"
VERSION=${1:-latest}

docker build -t ${APP_NAME}:${VERSION} .
```

## Optimizations

### Layer Caching

Order from least to most frequently changing:

```dockerfile
# ✅ Good
COPY package*.json ./
RUN npm install

COPY *.fsproj ./
RUN dotnet restore

COPY . .
RUN dotnet build

# ❌ Bad - invalidates all layers
COPY . .
RUN npm install
RUN dotnet build
```

### Smaller Images

```dockerfile
# Use Alpine
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

# Remove build artifacts
RUN rm -rf /app/obj /app/bin/Debug
```

## Checklist

- [ ] Multi-stage build used
- [ ] Build stage has SDK + Node.js
- [ ] Runtime stage only has aspnet runtime
- [ ] Static files copied from build stage
- [ ] Data directory created
- [ ] Health check configured
- [ ] Image tagged with version

## See Also

- `docker-compose.md` - Stack configuration
- `production.md` - Production settings
