---
name: tailscale-deploy
description: |
  Deploy F# full-stack applications with Tailscale sidecar for private network access without public ports.
  Use when deploying to production, setting up Docker compose with Tailscale, or need private networking.
  Creates docker-compose.yml with app + Tailscale sidecar pattern.
---

# Tailscale Sidecar Deployment

## When to Use This Skill

Activate when:
- User requests "deploy to production", "set up Tailscale"
- Need Docker deployment configuration
- Setting up private networking

## Architecture

```
Internet (blocked - no public ports)
    ↓
Tailscale Network (WireGuard encrypted)
    ↓
Tailscale Sidecar Container
    ↓ (internal network)
F# Application Container
```

## docker-compose.yml

```yaml
version: '3.8'

services:
  app:
    build: .
    container_name: my-app
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
    volumes:
      - ./data:/app/data
    networks:
      - app-network
    depends_on:
      - tailscale
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 3s
      retries: 3

  tailscale:
    image: tailscale/tailscale:latest
    container_name: my-app-tailscale
    hostname: my-app
    restart: unless-stopped
    environment:
      - TS_AUTHKEY=${TS_AUTHKEY}
      - TS_STATE_DIR=/var/lib/tailscale
      - TS_HOSTNAME=my-app
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

## Environment Configuration

### .env File (add to .gitignore!)

```bash
TS_AUTHKEY=tskey-auth-xxxxxxxxxxxxx
```

### Generate Tailscale Auth Key

1. Go to https://login.tailscale.com/admin/settings/keys
2. Click "Generate auth key"
3. Copy key to `.env` file

## Deployment Steps

```bash
# Build and deploy
docker-compose up -d

# Check Tailscale connection
docker exec my-app-tailscale tailscale status

# Access application
# From any Tailnet device: http://my-app
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `/dev/net/tun` not found | Run `modprobe tun` on host |
| Permission denied | Container needs `NET_ADMIN` |
| Auth key expired | Generate new key |

## Verification Checklist

- [ ] docker-compose.yml created
- [ ] .env file with TS_AUTHKEY
- [ ] .env in .gitignore
- [ ] Containers start successfully
- [ ] Tailscale shows connected
- [ ] App accessible via hostname
