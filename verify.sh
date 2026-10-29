#!/usr/bin/env bash
# BudgetBuddy: das eine Gate-Kommando (infrastructure-dvy29).
#
# Läuft immer alle sechs Schritte, auch wenn ein früher Schritt rot ist:
#   1. dotnet build            (BudgetBuddy.sln)
#   2. dotnet test             (Expecto, src/Tests)
#   3. npm run build           (vite build, Fable)
#   4. npm run guard:classes   (Tailwind-Klassen-Wächter)
#   5. npm run guard:tokens    (Token-Wächter)
#   6. npm run test:scripts    (node --test, Playwright-Wächter)
# Am Ende steht genau eine Zeile "VERIFY: PASS" oder "VERIFY: FAIL", bei FAIL mit
# den roten Schritten und den Namen der roten Tests. Exit 0 nur bei PASS.
#
# Übersprungene Pflichttests zählen als rot. Erlaubt sind nur die opt-in-
# Integrationstests gegen echte YNAB- und Comdirect-APIs ("(requires .env)").
#
# Parallelität: Expecto läuft in-memory, build und dist/ liegen je Worktree, das
# braucht kein Lock. Nur Schritt 6 ist serialisiert, über ein Lock im gemeinsamen
# git-Verzeichnis (für alle Worktrees derselbe Pfad). Grund: unter der Last
# zweier gleichzeitiger Läufe startet `vite preview` in export-design-system.mjs
# nicht innerhalb der fest eingestellten 5 Sekunden, der Test lädt dann zu früh
# und scheitert mit ERR_CONNECTION_REFUSED. Ein Lock eines nicht mehr laufenden
# Prozesses wird übernommen (scripts/verify-lib.sh).
#
# Kein set -e: Exit-Codes werden je Schritt geprüft.

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT" || exit 2
# shellcheck source=scripts/verify-lib.sh
source "$ROOT/scripts/verify-lib.sh"

if [ -t 1 ]; then
    RED=$'\033[0;31m'; GREEN=$'\033[0;32m'; YELLOW=$'\033[1;33m'; NC=$'\033[0m'
else
    RED=""; GREEN=""; YELLOW=""; NC=""
fi

LOG_DIR="$(mktemp -d "${TMPDIR:-/tmp}/bb-verify.XXXXXX")"
LOCK_DIR="$(vl_lock_path "$ROOT")"
KEEP_LOGS=0

cleanup() {
    vl_lock_release "$LOCK_DIR"
    if [ "$KEEP_LOGS" -eq 0 ]; then rm -rf "$LOG_DIR"; fi
}
trap cleanup EXIT
trap 'echo ""; echo "verify.sh abgebrochen (INT)"; exit 130' INT
trap 'echo ""; echo "verify.sh abgebrochen (TERM)"; exit 143' TERM

FAILED_STEPS=()
RED_TESTS=()
SUMMARY=()

fail_step() { FAILED_STEPS+=("$1"); echo "${RED}  ✗ $1: $2${NC}"; }
ok_step() { echo "${GREEN}  ✓ $1${NC}"; }

# run_step <nr> <name> <befehl...>: Ausgabe nach $LOG_DIR/<nr>.log, gibt den Exit-Code zurück.
run_step() {
    local nr="$1" name="$2" start rc
    shift 2
    echo ""
    echo "${YELLOW}[$nr/6]${NC} $name"
    start=$SECONDS
    "$@" > "$LOG_DIR/$nr.log" 2>&1
    rc=$?
    echo "  (Exit $rc, $((SECONDS - start))s, Log $LOG_DIR/$nr.log)"
    return "$rc"
}

show_tail() { tail -n "${2:-15}" "$1" | sed 's/^/    /'; }

echo "BudgetBuddy verify.sh in $ROOT"

# ── 1. dotnet build ─────────────────────────────────────────────────────
if run_step 1 "dotnet build" env DOTNET_CLI_UI_LANGUAGE=en dotnet build BudgetBuddy.sln --nologo -v q; then
    ok_step "dotnet build"
else
    grep -E "error [A-Z]+[0-9]+" "$LOG_DIR/1.log" | sort -u | head -20 | sed 's/^/    /'
    if grep -q "MSB3030" "$LOG_DIR/1.log"; then
        echo "    Hinweis: MSB3030 entsteht oft durch ein parallel laufendes dotnet watch. verify.sh erneut starten."
    fi
    fail_step "dotnet build" "Build fehlgeschlagen"
fi

# ── 2. dotnet test ──────────────────────────────────────────────────────
# DOTNET_CLI_UI_LANGUAGE=en: sonst lokalisiert dotnet test die Zusammenfassung
# und die Zähler sind nicht lesbar. Die trx-Datei liefert die Testnamen.
TRX="$LOG_DIR/dotnet.trx"
run_step 2 "dotnet test" env DOTNET_CLI_UI_LANGUAGE=en dotnet test src/Tests/Tests.fsproj --nologo \
    --results-directory "$LOG_DIR" --logger "trx;LogFileName=dotnet.trx"
DOTNET_EXIT=$?
D_TOTAL="$(vl_dotnet_count "$LOG_DIR/2.log" Total)"
D_PASSED="$(vl_dotnet_count "$LOG_DIR/2.log" Passed)"
D_FAILED="$(vl_dotnet_count "$LOG_DIR/2.log" Failed)"
D_SKIPPED="$(vl_dotnet_count "$LOG_DIR/2.log" Skipped)"
echo "  Total ${D_TOTAL:-?}, Passed ${D_PASSED:-?}, Failed ${D_FAILED:-?}, Skipped ${D_SKIPPED:-?}"

i=0
while IFS= read -r name; do
    [ -n "$name" ] || continue
    i=$((i + 1))
    RED_TESTS+=("dotnet: $name")
    echo "${RED}    ✗ $name${NC}"
    msg="$(vl_trx_first_message "$TRX" "$i")"
    [ -n "$msg" ] && echo "        $msg"
done < <(vl_trx_tests "$TRX" Failed)

OPTIONAL_SKIPS=0
MANDATORY_SKIPS=0
while IFS= read -r name; do
    [ -n "$name" ] || continue
    if vl_is_optional_skip "$name"; then
        OPTIONAL_SKIPS=$((OPTIONAL_SKIPS + 1))
    else
        MANDATORY_SKIPS=$((MANDATORY_SKIPS + 1))
        RED_TESTS+=("dotnet (übersprungen): $name")
        echo "${RED}    ﹣ übersprungen: $name${NC}"
    fi
done < <(vl_trx_tests "$TRX" NotExecuted)

if [ -z "$D_TOTAL" ] || [ -z "$D_PASSED" ] || [ -z "$D_FAILED" ] || [ -z "$D_SKIPPED" ]; then
    show_tail "$LOG_DIR/2.log"
    fail_step "dotnet test" "Zusammenfassung nicht lesbar, ein unlesbares Ergebnis ist kein bestandenes"
elif [ "$D_TOTAL" -eq 0 ]; then
    fail_step "dotnet test" "0 Tests gelaufen"
elif [ "$DOTNET_EXIT" -ne 0 ] || [ "$D_FAILED" -ne 0 ]; then
    [ "$D_FAILED" -eq 0 ] && show_tail "$LOG_DIR/2.log"
    fail_step "dotnet test" "$D_FAILED rot (Exit $DOTNET_EXIT)"
elif [ "$MANDATORY_SKIPS" -ne 0 ] || [ "$((D_SKIPPED - OPTIONAL_SKIPS))" -ne 0 ]; then
    fail_step "dotnet test" "$((D_SKIPPED - OPTIONAL_SKIPS)) Pflichttests übersprungen"
else
    ok_step "dotnet test: $D_PASSED grün, $OPTIONAL_SKIPS opt-in-Integrationstests übersprungen (requires .env)"
    SUMMARY+=("dotnet $D_PASSED grün")
fi

# ── 3. npm run build ────────────────────────────────────────────────────
if run_step 3 "npm run build" npm run build; then
    ok_step "npm run build"
else
    show_tail "$LOG_DIR/3.log" 25
    fail_step "npm run build" "vite build fehlgeschlagen"
fi

# ── 4./5. Wächter ───────────────────────────────────────────────────────
if run_step 4 "npm run guard:classes" npm run guard:classes; then
    ok_step "guard:classes"
else
    show_tail "$LOG_DIR/4.log" 25
    fail_step "guard:classes" "Verstöße gefunden"
fi

if run_step 5 "npm run guard:tokens" npm run guard:tokens; then
    ok_step "guard:tokens"
else
    show_tail "$LOG_DIR/5.log" 25
    fail_step "guard:tokens" "Verstöße gefunden"
fi

# ── 6. npm run test:scripts (unter Lock) ────────────────────────────────
echo ""
echo "Lock für test:scripts: $LOCK_DIR"
if vl_lock_acquire "$LOCK_DIR"; then
    run_step 6 "npm run test:scripts" npm run test:scripts
    NODE_EXIT=$?
    vl_lock_release "$LOCK_DIR"
    N_TESTS="$(vl_node_count "$LOG_DIR/6.log" tests)"
    N_PASS="$(vl_node_count "$LOG_DIR/6.log" pass)"
    N_FAIL="$(vl_node_count "$LOG_DIR/6.log" fail)"
    N_SKIPPED="$(vl_node_count "$LOG_DIR/6.log" skipped)"
    N_CANCELLED="$(vl_node_count "$LOG_DIR/6.log" cancelled)"
    echo "  tests ${N_TESTS:-?}, pass ${N_PASS:-?}, fail ${N_FAIL:-?}, cancelled ${N_CANCELLED:-?}, skipped ${N_SKIPPED:-?}"
    while IFS= read -r name; do
        [ -n "$name" ] || continue
        RED_TESTS+=("node: $name")
        echo "${RED}    ✗ $name${NC}"
    done < <(vl_node_failed_tests "$LOG_DIR/6.log")
    while IFS= read -r name; do
        [ -n "$name" ] || continue
        RED_TESTS+=("node (übersprungen): $name")
        echo "${RED}    ﹣ übersprungen: $name${NC}"
    done < <(vl_node_skipped_tests "$LOG_DIR/6.log")

    if [ -z "$N_TESTS" ] || [ -z "$N_PASS" ] || [ -z "$N_FAIL" ] || [ -z "$N_SKIPPED" ]; then
        show_tail "$LOG_DIR/6.log" 25
        fail_step "test:scripts" "Zusammenfassung nicht lesbar (Build im Lauf gescheitert?)"
    elif [ "$N_TESTS" -eq 0 ]; then
        fail_step "test:scripts" "0 Tests gelaufen"
    elif [ "$NODE_EXIT" -ne 0 ] || [ "$N_FAIL" -ne 0 ] || [ "${N_CANCELLED:-0}" -ne 0 ]; then
        [ "$N_FAIL" -eq 0 ] && show_tail "$LOG_DIR/6.log" 25
        fail_step "test:scripts" "$N_FAIL rot, ${N_CANCELLED:-?} abgebrochen (Exit $NODE_EXIT)"
    elif [ "$N_SKIPPED" -ne 0 ]; then
        fail_step "test:scripts" "$N_SKIPPED Pflichttests übersprungen"
    else
        ok_step "test:scripts: $N_PASS grün"
        SUMMARY+=("node $N_PASS grün")
    fi
else
    fail_step "test:scripts" "Lock $LOCK_DIR nicht erhalten, Schritt nicht gelaufen"
fi

# ── Ergebnis ────────────────────────────────────────────────────────────
echo ""
echo "══════════════════════════════════════════════"
if [ "${#FAILED_STEPS[@]}" -eq 0 ]; then
    echo "${GREEN}VERIFY: PASS (${SUMMARY[0]}, ${SUMMARY[1]}, dotnet build, npm run build, guard:classes, guard:tokens grün)${NC}"
    exit 0
fi
KEEP_LOGS=1
steps="${FAILED_STEPS[0]}"
for s in "${FAILED_STEPS[@]:1}"; do steps="$steps, $s"; done
echo "${RED}VERIFY: FAIL (rote Schritte: $steps)${NC}"
if [ "${#RED_TESTS[@]}" -gt 0 ]; then
    echo "Rote Tests (${#RED_TESTS[@]}):"
    for t in "${RED_TESTS[@]}"; do echo "  ✗ $t"; done
fi
echo "Logs: $LOG_DIR"
exit 1
