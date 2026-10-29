#!/usr/bin/env bash
# Einen frisch angelegten BudgetBuddy-Worktree baufähig machen (Harness-Migration M4,
# infrastructure-jcb2m). Vorlage: souls/scripts/worktree-prep.sh.
#
# Usage:
#   ./scripts/worktree-prep.sh <worktree> [--dry-run]
#
# Das Skript legt den Worktree nicht an. Das macht der Aufrufer vorher, etwa
#   git worktree add -b aw/<id> .worktrees/<id> HEAD
#
# Schritte (in dieser Reihenfolge):
#   1. Symlink <worktree>/node_modules -> <Haupt-Repo>/node_modules (übersprungen, wenn schon
#      vorhanden; Abbruch, wenn dort ein echter Ordner liegt).
#   2. Kopie von <Haupt-Repo>/.env nach <worktree>/.env (Warnung, wenn sie fehlt).
#
# Kein Build: ./verify.sh baut selbst (dotnet build, npm run build).
#
# Der Abbau läuft über scripts/worktree-teardown.sh: der node_modules-Symlink muss vor
# "git worktree remove --force" weg, sonst wird das echte node_modules geleert.
#
# --dry-run: gibt die geplanten Befehle aus, ohne etwas zu verändern. Der Worktree muss
# dafür noch nicht existieren (baseline-replay.sh prepare --dry-run legt ihn nicht an).

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Haupt-Repo: Elternordner des gemeinsamen .git, gleich ob das Skript aus einem Worktree läuft.
COMMON_GIT_DIR="$(git -C "$SCRIPT_DIR" rev-parse --path-format=absolute --git-common-dir)"
MAIN_ROOT="$(cd "$(dirname "$COMMON_GIT_DIR")" && pwd -P)"

DRY_RUN=0

die() {
    echo -e "${RED}Fehler: $*${NC}" >&2
    exit 1
}

usage() {
    echo "Usage: $0 <worktree> [--dry-run]" >&2
    exit 2
}

# Führt einen Befehl aus oder zeigt ihn im Dry-Run nur an.
run() {
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  [dry-run] $*"
    else
        echo "  + $*"
        "$@"
    fi
}

prep() {
    local wt="$1"

    if [ "$DRY_RUN" -eq 0 ]; then
        [ -d "$wt" ] || die "Worktree '$wt' existiert nicht oder ist kein Ordner."
        wt="$(cd "$wt" && pwd -P)"
        [ "$wt" != "$MAIN_ROOT" ] || die "'$wt' ist das Haupt-Repo selbst, kein Worktree."
    fi

    echo "Worktree vorbereiten: $wt"

    # Schritt 1: node_modules teilen statt neu installieren.
    if [ -L "$wt/node_modules" ]; then
        echo "  node_modules-Symlink existiert schon, übersprungen."
    elif [ -e "$wt/node_modules" ]; then
        die "$wt/node_modules existiert als echter Ordner, kein Symlink. Abbruch."
    else
        run ln -s "$MAIN_ROOT/node_modules" "$wt/node_modules"
    fi

    # Schritt 2: .env
    if [ -f "$MAIN_ROOT/.env" ]; then
        run cp "$MAIN_ROOT/.env" "$wt/.env"
    else
        echo -e "${YELLOW}  Warnung: $MAIN_ROOT/.env fehlt, keine Kopie.${NC}"
    fi

    echo ""
    if [ "$DRY_RUN" -eq 1 ]; then
        echo -e "${YELLOW}Dry-Run: nichts verändert.${NC}"
    else
        echo -e "${GREEN}Worktree bereit.${NC}"
    fi
}

main() {
    local args=()
    local arg
    for arg in "$@"; do
        if [ "$arg" = "--dry-run" ]; then
            DRY_RUN=1
        else
            args+=("$arg")
        fi
    done

    [ "${#args[@]}" -eq 1 ] || usage
    prep "${args[0]}"
}

main "$@"
