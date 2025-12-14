#!/bin/bash
#
# deploy-rules.sh - Import rules from rules.yml to the live Docker database
#
# Usage:
#   ./scripts/deploy-rules.sh "Budget Name"           # Add new rules
#   ./scripts/deploy-rules.sh "Budget Name" --clear   # Clear all and reimport
#   ./scripts/deploy-rules.sh --list                  # List available budgets
#
# Prerequisites:
#   - .env file with YNAB_TOKEN in project root
#   - Docker container running (will be stopped/started automatically)
#   - Volume mounted at ~/my_apps/budgetbuddy/
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
DATA_DIR="${HOME}/my_apps/budgetbuddy"
CONTAINER_NAME="budgetbuddy-app"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Show usage
usage() {
    echo "Usage: $0 <budget-name> [--clear]"
    echo "       $0 --list"
    echo ""
    echo "Options:"
    echo "  <budget-name>   Name of the YNAB budget to use"
    echo "  --clear         Delete all existing rules before import"
    echo "  --list          List available YNAB budgets"
    echo "  --help          Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 'My Budget'           # Add new rules only"
    echo "  $0 'My Budget' --clear   # Clear all rules and reimport"
    echo "  $0 --list                # Show available budgets"
}

# Check prerequisites
check_prerequisites() {
    if [[ ! -f "$PROJECT_DIR/.env" ]]; then
        log_error ".env file not found in $PROJECT_DIR"
        log_error "Please create .env with YNAB_TOKEN=your-token"
        exit 1
    fi

    if [[ ! -f "$PROJECT_DIR/rules.yml" ]]; then
        log_error "rules.yml not found in $PROJECT_DIR"
        exit 1
    fi

    if [[ ! -d "$DATA_DIR" ]]; then
        log_error "Data directory not found: $DATA_DIR"
        log_error "Is the Docker volume mounted correctly?"
        exit 1
    fi

    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet SDK not found. Please install .NET SDK."
        exit 1
    fi

    if ! command -v docker-compose &> /dev/null && ! command -v docker &> /dev/null; then
        log_error "docker-compose not found."
        exit 1
    fi
}

# Check if container is running
is_container_running() {
    docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"
}

# Stop the app container
stop_app() {
    if is_container_running; then
        log_info "Stopping $CONTAINER_NAME..."
        cd "$PROJECT_DIR" && docker-compose stop "$CONTAINER_NAME"
    else
        log_warn "Container $CONTAINER_NAME is not running"
    fi
}

# Start the app container
start_app() {
    log_info "Starting $CONTAINER_NAME..."
    cd "$PROJECT_DIR" && docker-compose start "$CONTAINER_NAME"

    # Wait for health check
    log_info "Waiting for app to be healthy..."
    for i in {1..30}; do
        if docker ps --format '{{.Names}} {{.Status}}' | grep "$CONTAINER_NAME" | grep -q "healthy"; then
            log_info "App is healthy!"
            return 0
        fi
        sleep 1
    done
    log_warn "App may not be fully healthy yet, check with: docker-compose ps"
}

# Run the import script
run_import() {
    local budget="$1"
    local clear_flag="$2"

    log_info "Running import script..."
    log_info "  Budget: $budget"
    log_info "  Clear: ${clear_flag:-no}"
    log_info "  Data dir: $DATA_DIR"

    cd "$PROJECT_DIR"

    if [[ -n "$clear_flag" ]]; then
        DATA_DIR="$DATA_DIR" dotnet fsi scripts/import-rules.fsx --clear "$budget"
    else
        DATA_DIR="$DATA_DIR" dotnet fsi scripts/import-rules.fsx "$budget"
    fi
}

# List budgets
list_budgets() {
    cd "$PROJECT_DIR"
    dotnet fsi scripts/import-rules.fsx --list
}

# Main
main() {
    # Handle --help
    if [[ "$1" == "--help" || "$1" == "-h" ]]; then
        usage
        exit 0
    fi

    # Handle --list
    if [[ "$1" == "--list" ]]; then
        check_prerequisites
        list_budgets
        exit 0
    fi

    # Require budget name
    if [[ -z "$1" ]]; then
        log_error "Budget name required"
        echo ""
        usage
        exit 1
    fi

    local budget="$1"
    local clear_flag=""

    # Check for --clear flag
    if [[ "$2" == "--clear" ]]; then
        clear_flag="--clear"
    fi

    check_prerequisites

    # Track if we stopped the container
    local was_running=false
    if is_container_running; then
        was_running=true
    fi

    # Stop, import, start
    stop_app

    if run_import "$budget" "$clear_flag"; then
        log_info "Import completed successfully!"
    else
        log_error "Import failed!"
        if $was_running; then
            start_app
        fi
        exit 1
    fi

    if $was_running; then
        start_app
    else
        log_warn "Container was not running before, not starting it"
    fi

    log_info "Done!"
}

main "$@"
