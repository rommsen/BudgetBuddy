#!/usr/bin/env bash
# Einen BudgetBuddy-Worktree unter .worktrees/ sicher abbauen (Harness-Migration M4,
# infrastructure-jcb2m). Vorlage: souls/scripts/worktree-teardown.sh.
#
# Usage:
#   ./scripts/worktree-teardown.sh <worktree> [--delete-branch] [--dry-run]
#
# <worktree> muss ein direkter Unterordner von <Haupt-Repo>/.worktrees/ sein (kein Punkt-Ordner wie
# .results). Ein relativer Pfad wird gegen das aktuelle Verzeichnis aufgelöst; existiert er dort nicht,
# gegen das Haupt-Repo (so geht ".worktrees/foo" auch aus einem Worktree heraus). Alles andere wird
# verweigert (Exit 1).
#
# Schritte (in dieser Reihenfolge, jeder einzeln, keiner per && verkettet):
#   1. Symlinks: jeden Symlink im Worktree entfernen und prüfen, dass keiner übrig ist.
#      Sonst leert "git worktree remove --force" das echte node_modules.
#   2. remove:   git worktree remove --force <worktree>        (nur wenn 1 geklappt hat)
#      Danach Reste: legt ein Editor-Sprachserver während des Entfernens obj/-Stubs neu an, scheitert
#      remove mit "Directory not empty" oder der Ordner taucht wieder auf. Ist der Ordner bei git nicht
#      mehr als Worktree eingetragen, enthält keinen Symlink und nichts außerhalb von obj/ und bin/,
#      wird er gelöscht. Sonst bleibt er stehen und der Schritt gilt als fehlgeschlagen.
#   3. branch:   git branch -D aw/<name>, nur mit --delete-branch und nur wenn 2 geklappt hat.
#      Ohne die Option bleibt der Branch stehen (er kann noch nicht integrierte Arbeit tragen).
#   4. prune:    git worktree prune                             (hängt von nichts ab, läuft immer)
# Ein Fehler stoppt nur die Folgeschritte, die davon abhängen; der Exit-Code ist dann 1.
#
# --dry-run: gibt die geplanten Schritte aus, ohne etwas zu verändern. Der Worktree muss dafür
# nicht existieren.

set -uo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMMON_GIT_DIR="$(git -C "$SCRIPT_DIR" rev-parse --path-format=absolute --git-common-dir)" || exit 1
MAIN_ROOT="$(cd "$(dirname "$COMMON_GIT_DIR")" && pwd -P)" || exit 1
WORKTREES_DIR="$MAIN_ROOT/.worktrees"

DRY_RUN=0
DELETE_BRANCH=0

die() {
    echo -e "${RED}Fehler: $*${NC}" >&2
    exit 1
}

usage() {
    echo "Usage: $0 <worktree> [--delete-branch] [--dry-run]" >&2
    exit 2
}

# Führt einen Befehl aus oder zeigt ihn im Dry-Run nur an. Gibt den Exit-Code des Befehls zurück.
run() {
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  [dry-run] $*"
        return 0
    fi
    echo "  + $*"
    "$@"
}

# Absoluten Pfad bestimmen: existierende Ordner physisch (pwd -P), sonst lexikalisch.
resolve_target() {
    local target="$1"
    local candidate
    case "$target" in
        /*) candidate="$target" ;;
        *)
            if [ -e "$PWD/$target" ]; then
                candidate="$PWD/$target"
            else
                candidate="$MAIN_ROOT/$target"
            fi
            ;;
    esac
    candidate="${candidate%/}"
    if [ -d "$candidate" ]; then
        (cd "$candidate" && pwd -P)
    else
        echo "$candidate"
    fi
}

# Ist <abs> bei git noch als Worktree eingetragen?
is_registered() {
    local abs="$1"
    git -C "$MAIN_ROOT" worktree list --porcelain | awk -v p="worktree $abs" '$0 == p { found = 1 } END { exit found ? 0 : 1 }'
}

# Gibt jeden Eintrag aus, der kein Ordner ist und nicht unter einem obj/- oder bin/-Ordner liegt.
non_build_residue() {
    local abs="$1"
    find "$abs" -mindepth 1 -not -type d | while IFS= read -r entry; do
        case "/${entry#"$abs"/}" in
            */obj/* | */bin/*) ;;
            *) echo "$entry" ;;
        esac
    done
}

# Reste nach "git worktree remove" behandeln. Rückgabe 0, wenn der Ordner danach weg ist.
clear_residue() {
    local abs="$1"
    [ -e "$abs" ] || return 0
    if is_registered "$abs"; then
        echo -e "${RED}  $abs ist noch als Worktree eingetragen, Reste bleiben stehen.${NC}" >&2
        return 1
    fi
    local links other
    links="$(find "$abs" -type l)"
    if [ -n "$links" ]; then
        echo -e "${RED}  Reste enthalten Symlinks, Ordner bleibt stehen:${NC}" >&2
        echo "$links" >&2
        return 1
    fi
    other="$(non_build_residue "$abs")"
    if [ -n "$other" ]; then
        echo -e "${RED}  Reste enthalten Dateien außerhalb von obj/ und bin/, Ordner bleibt stehen:${NC}" >&2
        echo "$other" | head -20 >&2
        return 1
    fi
    echo "  Nur obj/- und bin/-Reste übrig, Ordner wird gelöscht."
    run rm -rf "$abs"
}

teardown() {
    local target="$1"

    # Lexikalisch prüfen, bevor irgendetwas aufgelöst wird.
    case "$target" in
        *..*) die "Pfad '$target' enthält '..', verweigert." ;;
    esac

    local abs name
    abs="$(resolve_target "$target")"
    case "$abs" in
        "$WORKTREES_DIR"/*) ;;
        *) die "Pfad '$abs' liegt nicht unter $WORKTREES_DIR/, verweigert." ;;
    esac
    name="${abs#"$WORKTREES_DIR"/}"
    case "$name" in
        "" | */*) die "Pfad '$abs' ist kein Worktree direkt unter $WORKTREES_DIR/, verweigert." ;;
        .*) die "Punkt-Ordner '$name' ist kein Worktree, verweigert." ;;
    esac
    if [ "$DRY_RUN" -eq 0 ] && [ ! -d "$abs" ]; then
        die "Worktree '$abs' existiert nicht."
    fi

    local branch="aw/$name"
    local status=0
    local removed=0

    echo "Worktree abbauen: $abs"

    # Schritt 1: Symlinks entfernen und prüfen (find folgt Symlinks ohne -L nicht).
    echo "[1/4] Symlinks"
    local symlinks_ok=1
    if [ "$DRY_RUN" -eq 1 ]; then
        echo "  [dry-run] rm <jeden Symlink aus: find $abs -type l>"
        if [ -d "$abs" ]; then
            find "$abs" -type l | sed 's/^/    /'
        fi
        echo "  [dry-run] Prüfung: find $abs -type l ist leer"
    else
        local link
        while IFS= read -r link; do
            [ -n "$link" ] || continue
            run rm "$link" || symlinks_ok=0
        done < <(find "$abs" -type l)
        local left
        left="$(find "$abs" -type l)"
        if [ -n "$left" ]; then
            echo -e "${RED}  Symlinks übrig, worktree remove und branch entfallen:${NC}" >&2
            echo "$left" >&2
            symlinks_ok=0
        elif [ "$symlinks_ok" -eq 1 ]; then
            echo "  Keine Symlinks mehr im Worktree."
        fi
    fi

    # Schritt 2: Worktree entfernen (hängt von 1 ab), danach obj/-Reste behandeln.
    echo "[2/4] remove"
    if [ "$symlinks_ok" -eq 1 ]; then
        if ! run git -C "$MAIN_ROOT" worktree remove --force "$abs"; then
            echo -e "${YELLOW}  worktree remove meldet einen Fehler, prüfe Reste.${NC}"
        fi
        if [ "$DRY_RUN" -eq 1 ]; then
            echo "  [dry-run] Reste: rm -rf $abs, falls nicht mehr eingetragen und nur obj/ und bin/ übrig"
            removed=1
        elif clear_residue "$abs"; then
            removed=1
        else
            echo -e "${RED}  Worktree nicht vollständig entfernt, branch entfällt.${NC}" >&2
            status=1
        fi
    else
        echo -e "${YELLOW}  übersprungen (Symlink-Schritt fehlgeschlagen).${NC}"
        status=1
    fi

    # Schritt 3: Branch löschen (nur auf Wunsch, hängt von 2 ab).
    echo "[3/4] branch"
    if [ "$DELETE_BRANCH" -eq 0 ]; then
        echo "  ohne --delete-branch, $branch bleibt stehen."
    elif [ "$DRY_RUN" -eq 1 ]; then
        echo "  [dry-run] git -C $MAIN_ROOT branch -D $branch (falls vorhanden)"
    elif [ "$removed" -eq 1 ]; then
        if git -C "$MAIN_ROOT" show-ref --verify --quiet "refs/heads/$branch"; then
            run git -C "$MAIN_ROOT" branch -D "$branch" || status=1
        else
            echo "  Branch $branch gibt es nicht, nichts zu tun."
        fi
    else
        echo -e "${YELLOW}  übersprungen (remove fehlgeschlagen oder übersprungen).${NC}"
    fi

    # Schritt 4: Verwaltungsdaten aufräumen (unabhängig von den anderen Schritten).
    echo "[4/4] prune"
    run git -C "$MAIN_ROOT" worktree prune || status=1

    echo ""
    if [ "$DRY_RUN" -eq 1 ]; then
        echo -e "${YELLOW}Dry-Run: nichts verändert.${NC}"
    elif [ "$status" -eq 0 ]; then
        echo -e "${GREEN}Aufgeräumt.${NC}"
    else
        echo -e "${RED}Abbau unvollständig, siehe Meldungen oben.${NC}" >&2
    fi
    return "$status"
}

main() {
    local args=()
    local arg
    for arg in "$@"; do
        case "$arg" in
            --dry-run) DRY_RUN=1 ;;
            --delete-branch) DELETE_BRANCH=1 ;;
            *) args+=("$arg") ;;
        esac
    done

    [ "${#args[@]}" -eq 1 ] || usage
    teardown "${args[0]}"
}

main "$@"
