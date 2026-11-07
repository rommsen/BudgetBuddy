#!/usr/bin/env bash
# Agent-Wächter (PreToolUse-Hook für das Agent-Tool).
#
# Ein Agent mit `name` läuft als Teammate und bleibt nach seiner Arbeit als Prozess stehen.
# Ohne `name` endet er von selbst, fortsetzen geht per SendMessage an seine Id.
#
# Fail-open wie git-guard.sh: Fehlt jq oder ist das Eingabe-JSON kaputt, fällt keine
# Entscheidung, damit ein Defekt nicht jeden Agent-Aufruf sperrt.

command -v jq >/dev/null 2>&1 || exit 0

name="$(jq -r '.tool_input.name // empty' 2>/dev/null)" || exit 0
[ -z "$name" ] && exit 0

jq -n '{hookSpecificOutput: {
  hookEventName: "PreToolUse",
  permissionDecision: "deny",
  permissionDecisionReason: "Agent ohne name starten: mit name bleibt er als Teammate-Prozess stehen. Fortsetzen geht per SendMessage an die Agent-Id."
}}'
