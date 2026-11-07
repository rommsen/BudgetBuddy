#!/usr/bin/env bash
# Selbsttest für agent-guard.sh: füttert den Hook mit simulierten PreToolUse-JSON-Eingaben
# und prüft die Entscheidung (deny oder keine).
# Aufruf: bash .claude/hooks/agent-guard.test.sh   (Exit 0 = alle Fälle grün)

set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/agent-guard.sh"
BASH_BIN="${BASH:-/bin/bash}"

if ! command -v jq >/dev/null 2>&1; then
  echo "agent-guard.test.sh: jq fehlt, der Selbsttest braucht jq" >&2
  exit 1
fi

pass=0
fail=0

ok()  { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "FAIL: $*" >&2; }

# Erwartung: deny | none
expect() {
  local want="$1" label="$2" input="$3" out rc got
  out="$(printf '%s' "$input" | "$BASH_BIN" "$HOOK" 2>/dev/null)"
  rc=$?
  got="$(printf '%s' "$out" | jq -r '.hookSpecificOutput.permissionDecision // "none"' 2>/dev/null)"
  [ -n "$got" ] || got=none
  if [ "$rc" -eq 0 ] && [ "$got" = "$want" ]; then ok; else bad "$label: erwartet $want, bekam $got (Exit $rc)"; fi
}

expect deny "mit name" '{"tool_name":"Agent","tool_input":{"name":"worker-x","prompt":"p"}}'
expect none "ohne name" '{"tool_name":"Agent","tool_input":{"prompt":"p"}}'
expect none "leerer name" '{"tool_name":"Agent","tool_input":{"name":"","prompt":"p"}}'
expect none "kaputtes JSON" '{kaputt'

out="$(printf '%s' '{"tool_input":{"name":"x"}}' | PATH="/nonexistent-agent-guard-test" "$BASH_BIN" "$HOOK" 2>/dev/null)"
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$out" ]; then ok; else bad "jq fehlt: Exit $rc, Output '$out'"; fi

echo "agent-guard.test.sh: $pass bestanden, $fail fehlgeschlagen"
[ "$fail" -eq 0 ]
