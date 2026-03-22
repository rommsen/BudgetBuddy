#!/usr/bin/env bash
set -euo pipefail

# ============================================
# Setup: Bare Git repo + post-receive hook on docker-host
# Run this ONCE to set up the deploy pipeline.
# ============================================

REMOTE="docker-host"
BARE_REPO="/opt/repos/budgetbuddy.git"
STACK_DIR="/opt/stacks/budgetbuddy"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_DIR="$(dirname "$SCRIPT_DIR")"

echo "==> Creating bare repo on $REMOTE..."
ssh "$REMOTE" "sudo mkdir -p $BARE_REPO && sudo git init --bare $BARE_REPO"

echo "==> Setting ownership (so you can push without sudo)..."
ssh "$REMOTE" "sudo chown -R \$(whoami):\$(whoami) $BARE_REPO"

echo "==> Creating post-receive hook..."
ssh "$REMOTE" "cat > $BARE_REPO/hooks/post-receive" << 'HOOK'
#!/usr/bin/env bash
set -euo pipefail

STACK_DIR="/opt/stacks/budgetbuddy"
BUILD_DIR="/tmp/budgetbuddy-build"
IMAGE="budgetbuddy:latest"
LOG="/tmp/budgetbuddy-deploy.log"

exec > >(tee -a "$LOG") 2>&1
echo ""
echo "========================================"
echo "  BudgetBuddy Deploy — $(date)"
echo "========================================"

# Only deploy on main branch
while read oldrev newrev refname; do
    BRANCH=$(git rev-parse --symbolic --abbrev-ref "$refname" 2>/dev/null || echo "$refname")
    if [ "$BRANCH" != "main" ]; then
        echo "==> Push to $BRANCH, skipping deploy (only main triggers deploy)"
        exit 0
    fi
done

# Checkout source to temp build dir
echo "==> Checking out source..."
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
git --work-tree="$BUILD_DIR" checkout -f main

# Build Docker image (natively on x86!)
echo "==> Building Docker image (native)..."
cd "$BUILD_DIR"
docker build -t "$IMAGE" .

# Sync compose.yaml if it exists in the repo
if [ -f "$BUILD_DIR/compose.yaml" ]; then
    echo "==> Syncing compose.yaml to stack dir..."
    sudo cp "$BUILD_DIR/compose.yaml" "$STACK_DIR/compose.yaml"
elif [ -f "$STACK_DIR/compose.yaml" ]; then
    echo "==> Using existing compose.yaml in stack dir"
else
    echo "==> WARNING: No compose.yaml found!"
    exit 1
fi

# Restart stack
echo "==> Restarting stack..."
cd "$STACK_DIR"
docker compose up -d

# Wait for health check
echo "==> Waiting for health check (30s)..."
sleep 30

HEALTH=$(docker inspect --format='{{.State.Health.Status}}' budgetbuddy-app 2>/dev/null || echo "unknown")
if [ "$HEALTH" = "healthy" ]; then
    echo "==> Deploy successful! App is healthy."
else
    echo "==> WARNING: Health status: $HEALTH"
    echo "    Check logs: docker logs budgetbuddy-app"
fi

# Cleanup
rm -rf "$BUILD_DIR"

echo "==> Done."
HOOK

ssh "$REMOTE" "chmod +x $BARE_REPO/hooks/post-receive"

echo "==> Syncing initial compose.yaml to stack dir..."
scp "$REPO_DIR/compose.yaml" "$REMOTE:/tmp/budgetbuddy-compose.yaml"
ssh -t "$REMOTE" "sudo mkdir -p $STACK_DIR && sudo mv /tmp/budgetbuddy-compose.yaml $STACK_DIR/compose.yaml"

echo "==> Adding git remote 'deploy' locally..."
cd "$REPO_DIR"
if git remote get-url deploy &>/dev/null; then
    git remote set-url deploy "$REMOTE:$BARE_REPO"
    echo "    Updated existing 'deploy' remote"
else
    git remote add deploy "$REMOTE:$BARE_REPO"
    echo "    Added new 'deploy' remote"
fi

echo ""
echo "========================================"
echo "  Setup complete!"
echo "========================================"
echo ""
echo "  To deploy, simply run:"
echo "    git push deploy main"
echo ""
echo "  Or use: ./scripts/deploy.sh"
echo ""
