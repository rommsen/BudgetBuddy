---
name: fsharp-docker
description: |
  Create optimized Docker images for F# applications using multi-stage builds.
  Use when containerizing apps, optimizing image size, or fixing Docker issues.
  Ensures small, production-ready images with proper layer caching.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  - standards/deployment/docker.md
---

# Docker for F# Applications

## When to Use This Skill

Activate when:
- Creating Dockerfile for F# app
- Optimizing Docker image size
- Fixing Docker build issues
- User asks "how to containerize"
- Need production Docker image

## Multi-Stage Build Strategy

**Why multi-stage?**
- **Build stage**: Uses SDK image (large, has build tools)
- **Runtime stage**: Uses runtime image (small, production-ready)
- **Result**: Smaller final image (~200MB vs 2GB)

## Quick Start

### 1. Create Dockerfile

```dockerfile
# ============ BUILD STAGE ============
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project files and restore (layer caching!)
COPY src/Server/Server.fsproj src/Server/
COPY src/Shared/Shared.fsproj src/Shared/
WORKDIR /app/src/Server
RUN dotnet restore

# Copy source and build
WORKDIR /app
COPY . .
WORKDIR /app/src/Server
RUN dotnet publish -c Release -o /app/publish

# ============ RUNTIME STAGE ============
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy built app from build stage
COPY --from=build /app/publish .

# Setup data directory
ENV DATA_DIR=/app/data
RUN mkdir -p /app/data

# Expose port
EXPOSE 5000

# Run app
ENTRYPOINT ["dotnet", "Server.dll"]
```

### 2. Create .dockerignore

```
# Ignore build artifacts
**/bin/
**/obj/
**/.vs/

# Ignore data
data/
*.db
*.db-shm
*.db-wal

# Ignore local config
.env
*.local.json

# Ignore git
.git/
.gitignore
```

### 3. Build and Run

```bash
# Build image
docker build -t myapp:latest .

# Run container
docker run -d \
  --name myapp \
  -p 5000:5000 \
  -v myapp-data:/app/data \
  -e YNAB_TOKEN=your-token \
  myapp:latest

# View logs
docker logs -f myapp

# Stop container
docker stop myapp
docker rm myapp
```

## Optimization Tips

### Layer Caching

```dockerfile
# ✅ GOOD - Copy project files first, then restore
COPY src/Server/Server.fsproj src/Server/
RUN dotnet restore

# Then copy source and build
COPY . .
RUN dotnet publish

# ❌ BAD - Copy everything first
COPY . .
RUN dotnet restore  # Cache invalidated on ANY file change
```

### Multi-Project Solutions

```dockerfile
# Copy all project files for restore
COPY src/Server/Server.fsproj src/Server/
COPY src/Shared/Shared.fsproj src/Shared/
COPY src/Client/Client.fsproj src/Client/

# Restore from Server project (restores all dependencies)
WORKDIR /app/src/Server
RUN dotnet restore
```

### Smaller Images

```dockerfile
# Use Alpine for smallest images
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

# Or use runtime-deps (no ASP.NET, smallest)
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
```

## Troubleshooting

### Build Failures

```bash
# Check build stage
docker build --target build -t myapp:build .

# Interactive shell in build stage
docker run -it --rm myapp:build /bin/bash
```

### Runtime Issues

```bash
# Check logs
docker logs myapp

# Shell into running container
docker exec -it myapp /bin/bash

# Check environment
docker exec myapp env
```

### Permission Issues

```dockerfile
# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser /app
USER appuser
```

## Health Checks

```dockerfile
# Add health check
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1
```

## Checklist

- [ ] **Read** `standards/deployment/docker.md`
- [ ] Multi-stage Dockerfile (build + runtime)
- [ ] .dockerignore file created
- [ ] Project files copied before source
- [ ] dotnet restore in separate layer
- [ ] Data directory with volume mount
- [ ] Environment variables configured
- [ ] Image builds successfully
- [ ] Container runs and serves traffic

## Common Mistakes

❌ **Single-stage build:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0
COPY . .
RUN dotnet publish
ENTRYPOINT ["dotnet", "run"]  # 2GB image!
```

✅ **Multi-stage build:**
```dockerfile
FROM sdk:8.0 AS build
# ... build
FROM aspnet:8.0
COPY --from=build /app/publish .  # ~200MB image
```

❌ **No layer caching:**
```dockerfile
COPY . .
RUN dotnet restore  # Runs on every change!
```

✅ **Cache dependencies:**
```dockerfile
COPY *.fsproj .
RUN dotnet restore  # Cached unless project file changes
COPY . .
```

## Related Skills

- **tailscale-deploy** - Full deployment with Tailscale
- **fsharp-backend** - App being containerized

## Detailed Documentation

For complete Docker patterns:
- `standards/deployment/docker.md` - Complete Docker guide
- `standards/deployment/docker-compose.md` - Orchestration
- `standards/deployment/production.md` - Production checklist
