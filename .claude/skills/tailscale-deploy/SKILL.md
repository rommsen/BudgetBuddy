---
name: tailscale-deploy
description: |
  Deploy F# full-stack app using Docker + Tailscale for secure remote access.
  Use when deploying to production or setting up Docker-based deployment.
  Ensures proper containerization, volume management, and secure networking.
  Creates Dockerfile and docker-compose.yml.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  required-reading:
    - standards/deployment/production.md
  workflow:
    - step: 1
      file: standards/deployment/docker.md
      purpose: Create Dockerfile
      output: Dockerfile
    - step: 2
      file: standards/deployment/docker-compose.md
      purpose: Define services
      output: docker-compose.yml
    - step: 3
      file: standards/deployment/tailscale.md
      purpose: Configure Tailscale sidecar
      output: docker-compose.yml (updated)
---

# Docker + Tailscale Deployment

## When to Use This Skill

Activate when:
- User requests "deploy the app"
- Setting up production deployment
- Need secure remote access
- Configuring Docker containers
- Project needs containerization

## Deployment Overview

```
Docker Container (App)
    ├── Multi-stage build (optimized)
    ├── Volume mount (persistent data)
    └── Tailscale sidecar (secure access)
```

**Key Features:**
- Multi-stage Docker build (small image)
- Persistent data with volumes
- Secure access via Tailscale
- Environment variable configuration

## Implementation Workflow

### Step 1: Create Dockerfile

**Read:** `standards/deployment/docker.md`
**Create:** `Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy and restore
COPY src/Server/Server.fsproj src/Server/
COPY src/Shared/Shared.fsproj src/Shared/
WORKDIR /app/src/Server
RUN dotnet restore

# Copy source and build
WORKDIR /app
COPY . .
WORKDIR /app/src/Server
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Data directory
ENV DATA_DIR=/app/data
RUN mkdir -p /app/data

EXPOSE 5000
ENTRYPOINT ["dotnet", "Server.dll"]
```

**Key:** Multi-stage build keeps image small

---

### Step 2: Create docker-compose.yml

**Read:** `standards/deployment/docker-compose.md`
**Create:** `docker-compose.yml`

```yaml
version: '3.8'

services:
  app:
    build: .
    container_name: budgetbuddy
    restart: unless-stopped
    volumes:
      - app-data:/app/data
    environment:
      - ASPNETCORE_URLS=http://+:5000
      - DATA_DIR=/app/data
      - YNAB_TOKEN=${YNAB_TOKEN}
    network_mode: service:tailscale

  tailscale:
    image: tailscale/tailscale:latest
    container_name: budgetbuddy-tailscale
    hostname: budgetbuddy
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_SERVE_CONFIG=/config/serve.json
    volumes:
      - tailscale-state:/var/lib/tailscale
      - ./serve.json:/config/serve.json:ro
    cap_add:
      - NET_ADMIN
      - SYS_MODULE

volumes:
  app-data:
  tailscale-state:
```

**Key:** App shares network with Tailscale sidecar

---

### Step 3: Configure Tailscale Serve

**Read:** `standards/deployment/tailscale.md`
**Create:** `serve.json`

```json
{
  "TCP": {
    "443": {
      "HTTPS": true
    }
  },
  "Web": {
    "${TS_CERT_DOMAIN}:443": {
      "Handlers": {
        "/": {
          "Proxy": "http://127.0.0.1:5000"
        }
      }
    }
  }
}
```

**Key:** Tailscale handles HTTPS, proxies to app

---

### Step 4: Deploy

**Commands:**

```bash
# 1. Set environment variables
export YNAB_TOKEN="your-token"
export TS_AUTHKEY="your-tailscale-auth-key"

# 2. Build and start
docker-compose up -d

# 3. Check logs
docker-compose logs -f app

# 4. Access via Tailscale URL
# https://budgetbuddy.your-tailnet.ts.net
```

---

## Quick Reference

### Docker Commands

```bash
# Build and start
docker-compose up -d

# View logs
docker-compose logs -f app
docker-compose logs -f tailscale

# Restart
docker-compose restart app

# Stop
docker-compose down

# Rebuild after code changes
docker-compose up -d --build
```

### Environment Variables

Required in `.env` or export:
```bash
YNAB_TOKEN=your-ynab-token
TS_AUTHKEY=your-tailscale-auth-key
DATA_DIR=/app/data  # Inside container
```

### Data Persistence

```bash
# Backup data
docker run --rm -v budgetbuddy_app-data:/data -v $(pwd):/backup \
  alpine tar czf /backup/data-backup.tar.gz -C /data .

# Restore data
docker run --rm -v budgetbuddy_app-data:/data -v $(pwd):/backup \
  alpine tar xzf /backup/data-backup.tar.gz -C /data
```

## Verification Checklist

- [ ] **Read standards** (production.md, docker.md)
- [ ] Dockerfile with multi-stage build
- [ ] docker-compose.yml with app + tailscale
- [ ] serve.json for Tailscale HTTPS
- [ ] Environment variables configured
- [ ] Data volumes defined
- [ ] `.env` file with secrets (not committed!)
- [ ] App accessible via Tailscale URL
- [ ] Logs show no errors

## Common Pitfalls

**Most Critical:**
- ❌ Committing secrets to git (.env, auth keys)
- ❌ Not mounting data volume (data loss on restart)
- ❌ Wrong network mode (app can't reach Tailscale)
- ❌ Missing environment variables
- ✅ Use .env file (add to .gitignore)
- ✅ Mount volumes for persistent data
- ✅ Use `network_mode: service:tailscale`

## Troubleshooting

```bash
# Check container status
docker-compose ps

# Check app is running
docker-compose exec app dotnet --version

# Check Tailscale status
docker-compose exec tailscale tailscale status

# View all logs
docker-compose logs

# Restart everything
docker-compose down && docker-compose up -d
```

## Related Skills

- **fsharp-backend** - App being deployed
- **fsharp-feature** - Features deployed to production

## Detailed Documentation

For complete patterns and examples:
- `standards/deployment/production.md` - Production checklist
- `standards/deployment/docker.md` - Dockerfile patterns
- `standards/deployment/docker-compose.md` - Compose configuration
- `standards/deployment/tailscale.md` - Tailscale setup
