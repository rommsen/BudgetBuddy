# scripts/verify-lib.sh
#
# Hilfsfunktionen für ./verify.sh (infrastructure-dvy29). Wird per `source`
# geladen, setzt keine Shell-Optionen und gibt nichts aus, solange keine
# Funktion aufgerufen wird. Läuft mit der bash 3.2 von macOS (keine
# assoziativen Arrays, kein mapfile). Tests: scripts/verify-lib.test.mjs.

# ── Testausgaben lesen ──────────────────────────────────────────────────

# Zahl hinter "ℹ <label> N" aus der Ausgabe von `node --test`. Leer, wenn das
# Label fehlt: ein unlesbares Ergebnis ist kein Nullwert.
vl_node_count() {
    grep -E "^ℹ $2 [0-9]+$" "$1" 2>/dev/null | tail -1 | grep -oE '[0-9]+$' | tr -d '\n'
}

# Rote Tests aus dem Abschnitt "✖ failing tests:" am Ende der spec-Ausgabe,
# eine Zeile je Test: "<name> (<datei>:<zeile>:<spalte>)".
vl_node_failed_tests() {
    awk '
        /^✖ failing tests:/ { section = 1; next }
        !section { next }
        /^test at / { loc = substr($0, 9); next }
        /^✖ / {
            name = substr($0, length("✖ ") + 1)
            sub(/ \([0-9.]+m?s\)$/, "", name)
            if (loc != "") print name " (" loc ")"; else print name
            loc = ""
        }
    ' "$1" 2>/dev/null
}

# Übersprungene Tests ("﹣ <name> (<dauer>) # <grund>"), eine Zeile je Test.
vl_node_skipped_tests() {
    awk '
        /^[[:space:]]*﹣ / {
            line = $0
            sub(/^[[:space:]]*﹣ /, "", line)
            reason = ""
            if (match(line, / # .*$/)) { reason = substr(line, RSTART); line = substr(line, 1, RSTART - 1) }
            sub(/ \([0-9.]+m?s\)$/, "", line)
            print line reason
        }
    ' "$1" 2>/dev/null
}

# Zahl hinter "<Label>: N" in der Zusammenfassung von `dotnet test`
# (DOTNET_CLI_UI_LANGUAGE=en). Leer, wenn nicht gefunden.
vl_dotnet_count() {
    grep -oE "$2: +[0-9]+" "$1" 2>/dev/null | tail -1 | grep -oE '[0-9]+' | tr -d '\n'
}

# Testnamen mit dem gegebenen outcome (Failed, NotExecuted, Passed) aus der trx-Datei.
vl_trx_tests() {
    local trx="$1" xp count i
    xp="//*[local-name()='UnitTestResult'][@outcome='$2']"
    [ -s "$trx" ] || return 0
    count=$(xmllint --xpath "count($xp)" "$trx" 2>/dev/null)
    count=${count%%.*}
    [ -n "$count" ] || return 0
    i=1
    while [ "$i" -le "$count" ]; do
        # $(...) entfernt den Zeilenumbruch, den neuere xmllint-Versionen anhängen.
        printf '%s\n' "$(xmllint --xpath "string(($xp)[$i]/@testName)" "$trx" 2>/dev/null)"
        i=$((i + 1))
    done
}

# Erste nichtleere Zeile der Fehlermeldung des n-ten roten Tests (1-basiert).
vl_trx_first_message() {
    local xp="//*[local-name()='UnitTestResult'][@outcome='Failed']"
    xmllint --xpath "string(($xp)[$2]/*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message'])" "$1" 2>/dev/null \
        | grep -v '^[[:space:]]*$' | head -1
}

# Übersprungene dotnet-Tests, die erlaubt sind: nur die opt-in-Integrationstests
# gegen echte YNAB- und Comdirect-APIs (testList "... (requires .env)",
# RUN_INTEGRATION_TESTS=true). Jeder andere übersprungene Test ist ein Pflichttest.
vl_is_optional_skip() {
    case "$1" in
        *"(requires .env)"*) return 0 ;;
    esac
    return 1
}

# ── Lock um npm run test:scripts ────────────────────────────────────────
#
# Das Lock ist ein Verzeichnis (mkdir ist atomar) mit den Dateien pid, started
# (Startzeit des Halters laut ps, gegen wiederverwendete PIDs) und cwd.
# Verwaist ist es, wenn der Halter nicht mehr läuft oder seine PID inzwischen
# einem anderen Prozess gehört.

# Lock-Pfad im gemeinsamen git-Verzeichnis: Haupt-Checkout und alle Worktrees
# sehen denselben Pfad. Ohne git: ein fester Pfad unter TMPDIR.
vl_lock_path() {
    local common
    common="$(git -C "$1" rev-parse --path-format=absolute --git-common-dir 2>/dev/null)"
    if [ -n "$common" ]; then
        echo "$common/bb-verify-test-scripts.lock"
    else
        echo "${TMPDIR:-/tmp}/bb-verify-test-scripts.lock"
    fi
}

vl_proc_started() {
    ps -o lstart= -p "$1" 2>/dev/null | sed 's/^ *//; s/ *$//'
}

# 0, wenn der in <lockdir> notierte Halter noch lebt (gleiche PID, gleiche Startzeit).
vl_lock_holder_alive() {
    local pid started now
    pid="$(cat "$1/pid" 2>/dev/null)"
    [ -n "$pid" ] || return 1
    now="$(vl_proc_started "$pid")"
    [ -n "$now" ] || return 1
    started="$(cat "$1/started" 2>/dev/null)"
    # Ohne started-Datei (Halter schreibt sie gerade) zählt allein die PID.
    [ -z "$started" ] || [ "$started" = "$now" ]
}

# vl_lock_acquire <lockdir>
# Wartet, bis das Lock frei ist, und nimmt es für diese Shell ($$).
# VERIFY_LOCK_POLL_SECONDS (Vorgabe 5) und VERIFY_LOCK_MAX_WAIT_SECONDS
# (Vorgabe 3600, danach Rückgabe 1) steuern das Warten.
vl_lock_acquire() {
    local dir="$1" poll="${VERIFY_LOCK_POLL_SECONDS:-5}" max="${VERIFY_LOCK_MAX_WAIT_SECONDS:-3600}"
    local waited=0 empty_seen=0 holder_pid holder_cwd stale
    mkdir -p "$(dirname "$dir")" 2>/dev/null
    while ! mkdir "$dir" 2>/dev/null; do
        holder_pid="$(cat "$dir/pid" 2>/dev/null)"
        holder_cwd="$(cat "$dir/cwd" 2>/dev/null)"
        if [ -z "$holder_pid" ] && [ "$empty_seen" -eq 0 ]; then
            # Halter hat mkdir gemacht, pid aber noch nicht geschrieben: einmal abwarten.
            empty_seen=1
        elif ! vl_lock_holder_alive "$dir"; then
            echo "Lock $dir gehört keinem laufenden Prozess mehr (PID ${holder_pid:-unbekannt}, cwd ${holder_cwd:-unbekannt}), wird übernommen."
            # Atomar beiseiteschieben, damit zwei Wartende nicht beide löschen.
            stale="$dir.stale.$$"
            if mv "$dir" "$stale" 2>/dev/null; then
                rm -rf "$stale"
            fi
            empty_seen=0
            continue
        else
            empty_seen=0
        fi
        if [ "$waited" -ge "$max" ]; then
            echo "Lock $dir nach ${waited}s nicht erhalten, Halter PID ${holder_pid:-unbekannt}, cwd ${holder_cwd:-unbekannt}." >&2
            return 1
        fi
        # Meldung beim ersten Mal und danach etwa alle 30 Sekunden.
        if [ $((waited % 30)) -lt "$poll" ]; then
            echo "test:scripts läuft schon in einem anderen verify.sh: PID ${holder_pid:-unbekannt}, cwd ${holder_cwd:-unbekannt}. Warte (${waited}s bisher)."
        fi
        sleep "$poll"
        waited=$((waited + poll))
    done
    vl_proc_started "$$" > "$dir/started"
    pwd > "$dir/cwd"
    echo "$$" > "$dir/pid"
    return 0
}

# vl_lock_release <lockdir>: entfernt das Lock nur, wenn es dieser Shell gehört.
vl_lock_release() {
    if [ "$(cat "$1/pid" 2>/dev/null)" = "$$" ]; then
        rm -rf "$1"
    fi
    return 0
}
