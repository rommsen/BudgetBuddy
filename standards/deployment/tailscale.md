# Tailscale Integration

> Secure private networking for home server deployments.

## Overview

Tailscale provides secure, private networking without exposing ports to the internet. Each app gets its own hostname on your private Tailnet.

## When to Use This

- Home server deployments
- Secure remote access
- Multi-device access (laptop, phone, etc.)
- Private networking without VPN setup

## Patterns

### Sidecar Setup

```yaml
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
    - TS_EXTRA_ARGS=--advertise-tags=tag:home-server
  volumes:
    - tailscale-state:/var/lib/tailscale
    - /dev/net/tun:/dev/net/tun
  cap_add:
    - NET_ADMIN
    - SYS_MODULE
  networks:
    - app-network
```

### Generate Auth Key

1. Go to https://login.tailscale.com/admin/settings/keys
2. Create auth key:
   - ✅ Reusable (for multiple deployments)
   - ✅ Ephemeral (expires when offline) - for testing
   - Add tags: `tag:home-server`

### Access Control Lists (ACLs)

Configure at https://login.tailscale.com/admin/acls

```json
{
  "tagOwners": {
    "tag:home-server": ["your-email@example.com"]
  },
  "acls": [
    {
      "action": "accept",
      "src": ["your-email@example.com"],
      "dst": ["tag:home-server:*"]
    }
  ]
}
```

## Accessing Your App

### By Hostname

```bash
# MagicDNS (enable in Tailscale admin)
http://my-app

# Full domain
http://my-app.your-tailnet.ts.net
```

### By IP

```bash
# Get Tailscale IP
docker exec my-app-tailscale tailscale status

# Access
http://100.x.x.x:5000
```

## Monitoring

### Check Status

```bash
# Status
docker exec my-app-tailscale tailscale status

# Network diagnostics
docker exec my-app-tailscale tailscale netcheck

# Ping another device
docker exec my-app-tailscale tailscale ping other-device
```

### Logs

```bash
docker logs my-app-tailscale
```

## Multiple Apps

Each app gets its own sidecar:

```yaml
services:
  app1:
    # ...
  app1-tailscale:
    hostname: app1
    environment:
      - TS_HOSTNAME=app1
    volumes:
      - app1-tailscale:/var/lib/tailscale

  app2:
    # ...
  app2-tailscale:
    hostname: app2
    environment:
      - TS_HOSTNAME=app2
    volumes:
      - app2-tailscale:/var/lib/tailscale
```

Access:
- `http://app1`
- `http://app2`

## Troubleshooting

### Container Not Starting

```bash
# Check logs
docker logs my-app-tailscale

# Common issues:
# 1. Missing /dev/net/tun → Check host TUN/TAP support
# 2. Invalid auth key → Generate new key
# 3. Missing NET_ADMIN → Check cap_add in compose
```

### Can't Reach App

```bash
# 1. Check Tailscale connected
docker exec my-app-tailscale tailscale status

# 2. Test network
docker exec my-app-tailscale ping app

# 3. Verify app listening
docker exec my-app netstat -tlnp
```

## Security Best Practices

- Use ephemeral keys for testing
- Tag devices for ACL management
- Restrict access with ACLs
- Rotate auth keys regularly (every 90 days)
- Enable MagicDNS for easier access

## Checklist

- [ ] Auth key generated
- [ ] TS_AUTHKEY in .env
- [ ] /dev/net/tun mounted
- [ ] NET_ADMIN capability added
- [ ] Hostname configured
- [ ] Tags configured
- [ ] ACLs configured
- [ ] MagicDNS enabled

## See Also

- `docker-compose.md` - Stack setup
- Official docs: https://tailscale.com/kb/1282/docker/
