# Production Configuration

> Production settings, health checks, and monitoring.

## Overview

Production configuration includes health endpoints, logging, monitoring, and backup strategies.

## When to Use This

- Production deployments
- Health monitoring
- Automated backups
- Log management

## Patterns

### appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

### Health Check Endpoint

```fsharp
let healthCheck : HttpHandler =
    fun next ctx ->
        // Check database, external services, etc.
        let isHealthy = true
        
        if isHealthy then
            Successful.OK "healthy" next ctx
        else
            ServerErrors.SERVICE_UNAVAILABLE "unhealthy" next ctx

let configureApp (app: IApplicationBuilder) =
    Persistence.ensureDataDir()
    Persistence.initializeDatabase()
    
    app.UseStaticFiles() |> ignore
    app.UseRouting() |> ignore
    
    app.UseEndpoints(fun endpoints ->
        endpoints.MapGet("/health", healthCheck) |> ignore
    ) |> ignore
    
    app.UseGiraffe(Api.webApp)
```

### Docker Health Check

In Dockerfile:

```dockerfile
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1
```

In docker-compose.yml:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
  interval: 30s
  timeout: 3s
  retries: 3
  start_period: 10s
```

## Monitoring

### View Logs

```bash
# All logs
docker logs my-app

# Follow logs
docker logs -f my-app

# Last 100 lines
docker logs --tail 100 my-app

# Since timestamp
docker logs --since 2024-01-01T00:00:00 my-app
```

### Resource Usage

```bash
# Container stats
docker stats my-app

# Disk usage
docker system df
```

### Health Status

```bash
docker inspect --format='{{.State.Health.Status}}' my-app
```

## Backup and Restore

### Automated Backup

```bash
#!/bin/bash
set -e

BACKUP_DIR="/backups/my-app"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DATA_DIR="/opt/my-app/data"

mkdir -p ${BACKUP_DIR}

# Backup data
tar -czf ${BACKUP_DIR}/data_${TIMESTAMP}.tar.gz -C ${DATA_DIR} .

# Keep last 7 days
find ${BACKUP_DIR} -name "data_*.tar.gz" -mtime +7 -delete

echo "Backup complete: data_${TIMESTAMP}.tar.gz"
```

Add to crontab:

```bash
# Daily backup at 2 AM
0 2 * * * /opt/my-app/backup.sh >> /var/log/my-app-backup.log 2>&1
```

### Restore from Backup

```bash
#!/bin/bash
BACKUP_FILE=$1
DATA_DIR="/opt/my-app/data"

# Stop app
docker-compose down

# Restore data
tar -xzf ${BACKUP_FILE} -C ${DATA_DIR}

# Start app
docker-compose up -d

echo "Restore complete"
```

## Deployment

### Initial Deploy

```bash
# Build image
docker build -t my-app:v1.0.0 .

# Start stack
docker-compose up -d

# Verify
curl http://localhost:5000/health
docker logs my-app
```

### Update Deployment

```bash
# Pull latest code
git pull origin main

# Rebuild
docker-compose down
docker-compose up -d --build

# Verify
curl http://localhost:5000/health
```

### Rolling Update (Zero Downtime)

```bash
# Build new version
docker build -t my-app:v2 .

# Start new container
docker run -d --name my-app-v2 -p 5001:5000 my-app:v2

# Test
curl http://localhost:5001/health

# Switch traffic (update reverse proxy/load balancer)
# Then stop old version
docker stop my-app-v1
docker rm my-app-v1
```

## Environment Variables

### Production .env

```bash
# Tailscale
TS_AUTHKEY=tskey-auth-xxxxxxxxxxxxx

# App
ASPNETCORE_ENVIRONMENT=Production

# Custom app settings
DATA_DIR=/app/data
LOG_LEVEL=Information
```

**⚠️ Never commit .env to git**

## Best Practices

1. **Use multi-stage builds** - Smaller images
2. **Version your images** - Tag with version, not just `latest`
3. **Persist data in volumes** - Never store in containers
4. **Health checks** - Always include health endpoints
5. **Structured logging** - Log to stdout for Docker
6. **Secrets management** - Use env vars or secrets manager
7. **Automated backups** - Schedule regular backups
8. **Monitoring** - Set up alerts for failures
9. **Resource limits** - Set memory/CPU limits
10. **Documentation** - Keep deployment docs updated

## Checklist

- [ ] appsettings.Production.json configured
- [ ] Health endpoint implemented
- [ ] Docker health check configured
- [ ] Logging configured (stdout)
- [ ] Environment variables in .env
- [ ] Backup script created and scheduled
- [ ] Data volume mounted
- [ ] Resource limits set (optional)
- [ ] Monitoring enabled
- [ ] Deployment documented

## See Also

- `docker.md` - Dockerfile patterns
- `docker-compose.md` - Stack configuration
- `../backend/error-handling.md` - Error handling
