# Docker Compose

> Orchestrating F# app with Tailscale sidecar.

## Overview

Docker Compose defines the complete stack: application container + Tailscale sidecar for private networking.

## When to Use This

- Local development with containers
- Production deployment
- Multiple services (app + Tailscale)

## Patterns

### Basic Stack

```yaml
version: '3.8'

services:
  app:
    build: .
    container_name: my-app
    restart: unless-stopped
    ports:
      - "5000:5000"
    volumes:
      - ./data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
    networks:
      - app-network
    depends_on:
      - tailscale
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 3s
      retries: 3
      start_period: 10s

  tailscale:
    image: tailscale/tailscale:latest
    container_name: my-app-tailscale
    hostname: my-app
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=my-app
      - TS_ACCEPT_DNS=true
    volumes:
      - tailscale-data:/var/lib/tailscale
      - /dev/net/tun:/dev/net/tun
    cap_add:
      - NET_ADMIN
      - SYS_MODULE
    networks:
      - app-network

networks:
  app-network:
    driver: bridge

volumes:
  tailscale-data:
```

### Environment Variables

Create `.env` file:

```bash
# Tailscale
TS_AUTHKEY=tskey-auth-xxxxxxxxxxxxx

# App settings
ASPNETCORE_ENVIRONMENT=Production
```

**⚠️ Add `.env` to `.gitignore`**

## Deployment

### Start Stack

```bash
docker-compose up -d
```

### Stop Stack

```bash
docker-compose down
```

### Update and Restart

```bash
docker-compose down
docker-compose up -d --build
```

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f app
```

## Checklist

- [ ] Services defined (app + tailscale)
- [ ] Networks configured
- [ ] Volumes for persistence
- [ ] Health checks added
- [ ] Environment variables in .env
- [ ] .env in .gitignore
- [ ] Restart policies set

## See Also

- `docker.md` - Dockerfile patterns
- `tailscale.md` - Tailscale setup
- `production.md` - Production config
