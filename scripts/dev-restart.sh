#!/usr/bin/env bash
# Lokale Entwicklung frisch starten und sauber beenden.
#
# Usage:
#   ./scripts/dev-restart.sh            # Altes beenden, dann Backend und Frontend starten
#   ./scripts/dev-restart.sh --stop     # nur beenden
#   ./scripts/dev-restart.sh --dry-run  # nur zeigen, was beendet würde
#   npm run dev:restart
#
# Ports: Backend 5081 (oder BACKEND_PORT, wie in vite.config.js), Frontend 5181 (vite.config.js).
#
# Warum ein eigenes Skript statt nur `npm run dev:all`: `dotnet watch` beendet sich weder bei
# Ctrl+C noch bei einem SIGTERM an seine Hülle (npm, sh). Es hält den Absturz seiner App für
# normal und wartet auf eine Dateiänderung. `concurrently` erreicht mit SIGTERM und SIGKILL nur
# die Hülle, `dotnet watch` bleibt als Waise mit offener Ausgabe zurück, und das Terminal hängt.
# Darum startet dieses Skript `dev:all` in einer eigenen Prozessgruppe und beendet bei Ctrl+C die
# ganze Gruppe. Danach räumt es auf, was noch übrig ist:
#   - was auf den Dev-Ports lauscht, samt `dotnet watch` bzw. `concurrently` darüber,
#   - verwaiste `dotnet watch` mit Arbeitsverzeichnis src/Server dieses Repos (auch ohne Port),
#   - `concurrently` aus node_modules dieses Repos.
# Shells und Terminals bleiben unberührt.

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

MODE=start
case "${1:-}" in
  "") ;;
  --dry-run) MODE=dry-run ;;
  --stop) MODE=stop ;;
  *)
    echo -e "${RED}Unbekanntes Argument: $1${NC}" >&2
    echo "Usage: $0 [--stop|--dry-run]" >&2
    exit 1
    ;;
esac

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER_DIR="$REPO_ROOT/src/Server"
BACKEND_PORT="${BACKEND_PORT:-5081}"
FRONTEND_PORT=5181
PORTS=("$BACKEND_PORT" "$FRONTEND_PORT")
SELF_PGID="$(ps -o pgid= -p $$ | tr -d ' ')"

is_supervisor() {
  local cmd="$1"
  [[ "$cmd" == *dotnet-watch* || "$cmd" == *"dotnet watch"* || "$cmd" == *concurrently* ]]
}

listeners() {
  lsof -nP -t -iTCP:"$1" -sTCP:LISTEN 2>/dev/null || true
}

cwd_of() {
  lsof -a -p "$1" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p'
}

# Sammelt alle Prozesse, die zur Dev-Umgebung dieses Repos gehören, ohne die eigene Gruppe.
collect_targets() {
  local pid parent cmd
  local -a found=()
  for port in "${PORTS[@]}"; do
    for pid in $(listeners "$port"); do
      found+=("$pid")
      parent="$(ps -o ppid= -p "$pid" | tr -d ' ')"
      while [[ -n "$parent" && "$parent" != "1" && "$parent" != "0" ]]; do
        cmd="$(ps -o command= -p "$parent" 2>/dev/null || true)"
        [[ -z "$cmd" ]] && break
        is_supervisor "$cmd" && found+=("$parent")
        parent="$(ps -o ppid= -p "$parent" | tr -d ' ')"
      done
    done
  done
  # Verwaiste `dotnet watch` ohne Port: am Arbeitsverzeichnis erkennen.
  for pid in $(pgrep -f 'dotnet watch|dotnet-watch' 2>/dev/null || true); do
    [[ "$(cwd_of "$pid")" == "$SERVER_DIR" ]] && found+=("$pid")
  done
  for pid in $(pgrep -f "$REPO_ROOT/node_modules/.bin/concurrently" 2>/dev/null || true); do
    found+=("$pid")
  done
  for pid in "${found[@]+"${found[@]}"}"; do
    [[ "$pid" == "$$" ]] && continue
    [[ "$(ps -o pgid= -p "$pid" 2>/dev/null | tr -d ' ')" == "$SELF_PGID" ]] && continue
    echo "$pid"
  done | sort -u
}

stop_dev() {
  local targets alive late pid
  targets="$(collect_targets | tr '\n' ' ')"
  if [[ -z "${targets// /}" ]]; then
    echo -e "${GREEN}Nichts zu beenden, Ports ${PORTS[*]} sind frei.${NC}"
    return 0
  fi
  for pid in $targets; do
    echo "  $pid $(ps -o command= -p "$pid" 2>/dev/null | cut -c1-110)"
  done
  if [[ "$MODE" == "dry-run" ]]; then
    echo -e "${YELLOW}--dry-run: würde beenden: ${targets}${NC}"
    return 0
  fi
  echo -e "${YELLOW}Beende: ${targets}${NC}"
  # shellcheck disable=SC2086
  kill $targets 2>/dev/null || true
  alive=""
  for _ in $(seq 1 10); do
    alive=""
    for pid in $targets; do
      kill -0 "$pid" 2>/dev/null && alive="$alive $pid"
    done
    [[ -z "$alive" ]] && break
    sleep 1
  done
  if [[ -n "$alive" ]]; then
    echo -e "${YELLOW}Noch aktiv:$alive, beende hart.${NC}"
    # shellcheck disable=SC2086
    kill -9 $alive 2>/dev/null || true
    sleep 1
  fi
  # Was jetzt noch lauscht, hat ein Supervisor in der Zwischenzeit neu gestartet.
  for port in "${PORTS[@]}"; do
    late="$(listeners "$port")"
    if [[ -n "$late" ]]; then
      echo -e "${YELLOW}Port $port wieder belegt ($late), beende hart.${NC}"
      # shellcheck disable=SC2086
      kill -9 $late 2>/dev/null || true
    fi
  done
  sleep 1
  for port in "${PORTS[@]}"; do
    if [[ -n "$(listeners "$port")" ]]; then
      echo -e "${RED}Port $port ist weiter belegt.${NC}" >&2
      return 1
    fi
  done
  echo -e "${GREEN}Beendet, Ports ${PORTS[*]} sind frei.${NC}"
}

if [[ "$MODE" != "start" ]]; then
  stop_dev
  exit $?
fi

stop_dev

# `dev:all` in eigener Prozessgruppe (set -m), damit Ctrl+C sie als Ganzes beenden kann.
# stdin von /dev/null: ein Hintergrundprozess, der vom Terminal liest, würde angehalten.
cd "$REPO_ROOT"
echo -e "${GREEN}Starte Backend und Frontend (npm run dev:all), Ctrl+C beendet beides ...${NC}"
set -m
npm run dev:all < /dev/null &
DEV_PID=$!
set +m

shutdown() {
  trap - INT TERM
  echo
  echo -e "${YELLOW}Beende Dev-Umgebung ...${NC}"
  kill -TERM -- "-$DEV_PID" 2>/dev/null || true
  for _ in $(seq 1 5); do
    kill -0 -- "-$DEV_PID" 2>/dev/null || break
    sleep 1
  done
  kill -KILL -- "-$DEV_PID" 2>/dev/null || true
  stop_dev || true
  exit 0
}
trap shutdown INT TERM

# `wait` kehrt bei einem Signal zurück, dann läuft der Trap.
wait "$DEV_PID" || true
# dev:all ist von selbst geendet (etwa ein Absturz mit -k): Reste aufräumen.
trap - INT TERM
stop_dev || true
