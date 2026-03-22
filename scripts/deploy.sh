#!/usr/bin/env bash
set -euo pipefail

# ============================================
# Deploy BudgetBuddy to docker-host
#
# Pushes current main branch to the deploy remote.
# The post-receive hook on docker-host handles:
#   1. Native docker build (no cross-arch emulation!)
#   2. Stack restart via docker compose
#   3. Health check
#
# First-time setup: ./scripts/setup-deploy-remote.sh
# ============================================

REMOTE_NAME="deploy"
BRANCH="main"

# Check that deploy remote exists
if ! git remote get-url "$REMOTE_NAME" &>/dev/null; then
    echo "ERROR: Git remote '$REMOTE_NAME' not found."
    echo "Run ./scripts/setup-deploy-remote.sh first."
    exit 1
fi

# Check we're on main or allow explicit branch push
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ "$CURRENT_BRANCH" != "$BRANCH" ]; then
    echo "WARNING: You're on '$CURRENT_BRANCH', not '$BRANCH'."
    read -p "Push '$CURRENT_BRANCH' as '$BRANCH' to deploy? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Aborted."
        exit 0
    fi
    echo "==> Pushing $CURRENT_BRANCH -> $BRANCH on $REMOTE_NAME..."
    git push "$REMOTE_NAME" "$CURRENT_BRANCH:$BRANCH"
else
    echo "==> Pushing $BRANCH to $REMOTE_NAME..."
    git push "$REMOTE_NAME" "$BRANCH"
fi

echo ""
echo "==> Push complete. Build is running on docker-host."
echo "    Watch logs: ssh docker-host 'tail -f /tmp/budgetbuddy-deploy.log'"
