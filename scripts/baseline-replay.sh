#!/usr/bin/env bash
# Nachspiel einer erledigten Task für die Harness-Baseline vorbereiten und danach aufräumen.
# Vorlage: souls/scripts/baseline-replay.sh. Vorbereitung und Abbau laufen über
# scripts/worktree-prep.sh und scripts/worktree-teardown.sh (Harness-Migration M4, infrastructure-jcb2m).
#
# Usage:
#   ./scripts/baseline-replay.sh prepare <task-id> [--dry-run]
#   ./scripts/baseline-replay.sh teardown <worktree> [--dry-run]
#
# prepare:
#   1. Findet den Batch-Start-Commit der Task (neuester Commit mit "batch start" und "[<task-id>]",
#      auch bei mehreren Ids im Subject).
#   2. Findet den Ergebnis-Commit: chronologisch erster Nachfahre des Batch-Starts mit "[<task-id>]",
#      dessen Subject mit feature(, bug(, refactor( oder chore( beginnt und weder "batch start",
#      "capture", "bookkeeping", "promote" noch "refine" enthält.
#   3. Legt .worktrees/replay-<task-id>-<stamp> detached am Batch-Start an (relativ zum Haupt-Repo).
#   4. Vorbereitung über scripts/worktree-prep.sh: Symlink auf das gemeinsame node_modules,
#      Kopie von .env.
#   5. Gibt Worktree-Pfad, Task-Datei in doing/, Batch-Start-SHA und Ergebnis-SHA aus.
#
# teardown:
#   Verweigert Pfade außerhalb von .worktrees/replay-*, dann scripts/worktree-teardown.sh: entfernt
#   zuerst jeden Symlink im Worktree (sonst leert "git worktree remove --force" das echte
#   node_modules) und prüft, dass keiner übrig ist, dann git worktree remove --force und
#   git worktree prune. Jeder Schritt einzeln. Replay-Worktrees sind detached, es gibt keinen Branch.
#
# --dry-run: gibt die geplanten Befehle und aufgelösten SHAs aus, ohne etwas zu verändern.

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Checkout, in dem das Skript liegt (kann selbst ein Worktree sein); dessen HEAD ist die Suchbasis.
CHECKOUT_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
# Haupt-Repo: Elternordner des gemeinsamen .git, damit Replay-Worktrees nie verschachtelt landen.
COMMON_GIT_DIR="$(git -C "$SCRIPT_DIR" rev-parse --path-format=absolute --git-common-dir)"
MAIN_ROOT="$(dirname "$COMMON_GIT_DIR")"
WORKTREES_DIR="$MAIN_ROOT/.worktrees"

DRY_RUN=0

die() {
    echo -e "${RED}Fehler: $*${NC}" >&2
    exit 1
}

usage() {
    echo "Usage:" >&2
    echo "  $0 prepare <task-id> [--dry-run]" >&2
    echo "  $0 teardown <worktree> [--dry-run]" >&2
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

short() {
    git -C "$CHECKOUT_ROOT" rev-parse --short=7 "$1"
}

find_batch_start() {
    local task_id="$1"
    # Neuester Treffer zuerst; Feste-Zeichenketten-Vergleich, keine Regex auf der Id.
    # awk liest den Strom bis zum Ende, sonst bricht git log unter pipefail mit SIGPIPE ab.
    git -C "$CHECKOUT_ROOT" log --format='%H%x09%s' HEAD |
        awk -F '\t' -v tag="[$task_id]" '!found && index($2, "batch start") && index($2, tag) { print $1; found = 1 }'
}

find_result() {
    local task_id="$1"
    local batch="$2"
    local sha subject lower
    while IFS=$'\t' read -r sha subject; do
        case "$subject" in
            *"[$task_id]"*) ;;
            *) continue ;;
        esac
        case "$subject" in
            feature\(*|bug\(*|refactor\(*|chore\(*) ;;
            *) continue ;;
        esac
        lower="$(printf '%s' "$subject" | tr '[:upper:]' '[:lower:]')"
        case "$lower" in
            *"batch start"*|*capture*|*bookkeeping*|*promote*|*refine*) continue ;;
        esac
        echo "$sha"
        return 0
    done < <(git -C "$CHECKOUT_ROOT" log --reverse --format='%H%x09%s' "$batch..HEAD")
    return 0
}

cmd_prepare() {
    local task_id="$1"

    if ! [[ "$task_id" =~ ^[a-z0-9]+(-[a-z0-9]+)+$ ]]; then
        die "Ungültige Task-Id '$task_id' (erwartet z.B. ledger-ssmp3)."
    fi

    local batch result
    batch="$(find_batch_start "$task_id")"
    if [ -z "$batch" ]; then
        die "Kein Batch-Start-Commit für '$task_id' auf HEAD gefunden. Ist die Task-Id richtig und schon gelaufen?"
    fi
    result="$(find_result "$task_id" "$batch")"
    if [ -z "$result" ]; then
        die "Kein Ergebnis-Commit für '$task_id' nach Batch-Start $(short "$batch") gefunden."
    fi

    local task_file
    task_file="$(git -C "$CHECKOUT_ROOT" ls-tree -r --name-only "$batch" -- .agentheim/board |
        awk -v needle="/doing/$task_id-" '!found && index($0, needle) { print; found = 1 }')"
    if [ -z "$task_file" ]; then
        die "Task-Datei für '$task_id' liegt am Batch-Start $(short "$batch") nicht in einem doing/-Ordner."
    fi

    local stamp wt
    stamp="$(date +%Y%m%d-%H%M%S)"
    wt="$WORKTREES_DIR/replay-$task_id-$stamp"

    echo "Nachspiel vorbereiten für $task_id"
    echo "  Batch-Start: $(short "$batch")"
    echo "  Ergebnis:    $(short "$result")"
    echo ""

    run git -C "$MAIN_ROOT" worktree add --detach "$wt" "$batch"
    if [ "$DRY_RUN" -eq 1 ]; then
        "$SCRIPT_DIR/worktree-prep.sh" "$wt" --dry-run
    else
        "$SCRIPT_DIR/worktree-prep.sh" "$wt"
    fi

    echo ""
    if [ "$DRY_RUN" -eq 1 ]; then
        echo -e "${YELLOW}Dry-Run: nichts verändert.${NC}"
    else
        echo -e "${GREEN}Nachspiel bereit.${NC}"
    fi
    echo "WORKTREE=$wt"
    echo "TASK_FILE=$wt/$task_file"
    echo "BATCH_START=$(short "$batch")"
    echo "RESULT=$(short "$result")"
}

cmd_teardown() {
    local target="$1"

    # Lexikalisch prüfen, bevor irgendetwas aufgelöst wird.
    case "$target" in
        *..*) die "Pfad '$target' enthält '..', verweigert." ;;
    esac
    [ -d "$target" ] || die "Pfad '$target' existiert nicht oder ist kein Ordner."

    local abs real_worktrees
    abs="$(cd "$target" && pwd -P)"
    real_worktrees="$(cd "$WORKTREES_DIR" 2>/dev/null && pwd -P)" ||
        die "$WORKTREES_DIR existiert nicht."
    case "$abs" in
        "$real_worktrees"/replay-*) ;;
        *) die "Pfad '$abs' liegt nicht unter $real_worktrees/replay-*, verweigert." ;;
    esac
    case "${abs#"$real_worktrees"/}" in
        */*) die "Pfad '$abs' ist kein Replay-Worktree selbst, sondern ein Unterordner, verweigert." ;;
    esac

    echo "Nachspiel aufräumen: $abs"

    if [ "$DRY_RUN" -eq 1 ]; then
        "$SCRIPT_DIR/worktree-teardown.sh" "$abs" --dry-run
    else
        "$SCRIPT_DIR/worktree-teardown.sh" "$abs"
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

    [ "${#args[@]}" -eq 2 ] || usage

    case "${args[0]}" in
        prepare) cmd_prepare "${args[1]}" ;;
        teardown) cmd_teardown "${args[1]}" ;;
        *) usage ;;
    esac
}

main "$@"
