#!/usr/bin/env bash
set -euo pipefail

REMOTE="docker-host"
STACK_DIR="/opt/stacks/budgetbuddy"
IMAGE="budgetbuddy:latest"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"
HOMELAB_STACK="$REPO_DIR/../homelab/stacks/budgetbuddy"

echo "==> Building image for linux/amd64..."
docker build --platform linux/amd64 -t "$IMAGE" "$REPO_DIR"

echo "==> Transferring image to $REMOTE..."
docker save "$IMAGE" | ssh "$REMOTE" "docker load"

echo "==> Syncing compose.yaml..."
scp "$HOMELAB_STACK/compose.yaml" "$REMOTE:/tmp/budgetbuddy-compose.yaml"
ssh -t "$REMOTE" "sudo mv /tmp/budgetbuddy-compose.yaml $STACK_DIR/compose.yaml"

echo "==> Restarting stack..."
ssh "$REMOTE" "cd $STACK_DIR && docker compose up -d"

echo "==> Waiting for health check (30s)..."
sleep 30

HEALTH=$(ssh "$REMOTE" "docker inspect --format='{{.State.Health.Status}}' budgetbuddy-app" 2>/dev/null || echo "unknown")
if [ "$HEALTH" = "healthy" ]; then
    echo "==> ✓ budgetbuddy-app is healthy"
else
    echo "==> ⚠ Health status: $HEALTH"
    echo "    Check logs: ssh $REMOTE 'docker logs budgetbuddy-app'"
fi

echo "==> Deploy complete."
