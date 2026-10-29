#!/usr/bin/env bash
# Selbsttest für git-guard.sh: füttert den Hook mit simulierten PreToolUse-JSON-Eingaben
# und prüft die Entscheidung (deny, ask oder keine). Führt selbst kein git aus.
# Aufruf: bash .claude/hooks/git-guard.test.sh   (Exit 0 = alle Fälle grün)

set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK="$HERE/git-guard.sh"
BASH_BIN="${BASH:-/bin/bash}"

if ! command -v jq >/dev/null 2>&1; then
  echo "git-guard.test.sh: jq fehlt, der Selbsttest braucht jq zum Bauen der Eingaben" >&2
  exit 1
fi

pass=0
fail=0

ok()  { pass=$((pass + 1)); }
bad() { fail=$((fail + 1)); echo "FAIL: $*" >&2; }

bash_json() {
  jq -cn --arg c "$1" '{session_id:"t",hook_event_name:"PreToolUse",tool_name:"Bash",tool_input:{command:$c}}'
}

# Erwartung: deny | ask | none
expect() {
  local want="$1" cmd="$2" out rc got
  out="$(bash_json "$cmd" | "$BASH_BIN" "$HOOK" 2>/dev/null)"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    bad "[$cmd] Exit $rc statt 0"
    return
  fi
  if [ -z "$out" ]; then
    got="none"
  else
    got="$(printf '%s' "$out" | jq -r '.hookSpecificOutput.permissionDecision // "invalid"' 2>/dev/null || echo invalid)"
    if [ "$got" != "invalid" ]; then
      local ev reason
      ev="$(printf '%s' "$out" | jq -r '.hookSpecificOutput.hookEventName')"
      reason="$(printf '%s' "$out" | jq -r '.hookSpecificOutput.permissionDecisionReason // ""')"
      if [ "$ev" != "PreToolUse" ] || [ -z "$reason" ]; then
        bad "[$cmd] Ausgabe ohne hookEventName PreToolUse oder ohne Begründung: $out"
        return
      fi
    fi
  fi
  if [ "$got" = "$want" ]; then ok; else bad "[$cmd] erwartet $want, bekommen $got"; fi
}

# --- deny: Sammel-Staging und commit -a ---
expect deny 'git add -A'
expect deny 'git add .'
expect deny 'git add --all'
expect deny 'git -C /x/y add -A'
expect deny 'git commit -am "m"'
expect deny 'git commit -a -m m'
expect deny 'git commit --all'
expect deny 'cd x && git add .'
expect deny 'git add -u'
expect deny 'git add --update'
expect deny 'git add :/'
expect deny 'git add -- .'
expect deny 'git add ./'
expect deny 'git commit -av'
expect deny 'git -c user.name=x --git-dir=/r/.git --work-tree=/r add -A'
expect deny 'git --no-pager add --all'
expect deny 'ls; git add -A'
expect deny 'false || git commit -a -m x'
expect deny 'git status | git add .'
expect deny 'FOO=1 git add -A'
expect deny '/usr/bin/git add -A'
expect deny "git commit -a -m 'x' && git push"
# Formen aus Worktree-Läufen (infrastructure-dvy29)
expect deny 'git -C /Users/x/src/BudgetBuddy/.worktrees/t add -A'
expect deny 'git -C /Users/x/src/BudgetBuddy add .'
expect deny 'git -C /Users/x/src/BudgetBuddy add --all'
expect deny 'git -C /Users/x/src/BudgetBuddy commit -am "chore: x"'
expect deny 'cd /Users/x/src/BudgetBuddy && git add -A && git commit -m x'

# --- ask: jeder push ---
expect ask 'git push'
expect ask 'git -C /x push origin main'
expect ask 'git commit -m m && git push'
expect ask 'git push --dry-run'
expect ask 'git -C /wt/a push -u origin aw/x'

# --- keine Entscheidung ---
expect none 'git add src/A.fs ./src/B.fs'
expect none 'git add -- src/A.fs'
expect none 'git commit -m "add all"'
expect none 'git status'
expect none 'echo "git add -A"'
expect none 'git log --all'
expect none 'git add -u src/A.fs'
expect none 'git commit -m "-a"'
expect none 'git commit --message "-a all"'
expect none "echo 'git push' && git status"
# Befehle, die der Conductor bei der Integration nutzt, bleiben erlaubt
expect none 'git -C /Users/x/src/BudgetBuddy add -- .agentheim/board/a.md src/Server/Api.fs'
expect none 'git -C /Users/x/src/BudgetBuddy/.worktrees/t add -- src/A.fs'
expect none 'git merge --squash aw/infrastructure-dvy29'
expect none 'git worktree add .worktrees/t -b aw/t'
expect none 'git worktree remove .worktrees/t'
expect none 'git branch -D aw/infrastructure-dvy29'
expect none 'git -C /x commit -m "chore: y"'
expect none 'git diff --stat'
expect none 'git add verify.sh .claude/hooks/git-guard.sh .claude/settings.json'
expect none 'git -C /Users/x/src/BudgetBuddy commit -m "feat(infrastructure): y [infrastructure-dvy29]" -- verify.sh'
expect none 'git rev-parse --path-format=absolute --git-common-dir'
expect none 'git merge-tree --write-tree main aw/infrastructure-dvy29'

# --- ls ohne git: Exit 0, kein Output ---
expect none 'ls'

# --- Nicht-Bash-Tool: Exit 0, kein Output ---
out="$(jq -cn '{hook_event_name:"PreToolUse",tool_name:"Read",tool_input:{file_path:"/x"}}' | "$BASH_BIN" "$HOOK" 2>/dev/null)"
rc=$?
if [ "$rc" -eq 0 ] && [ -z "$out" ]; then ok; else bad "Nicht-Bash-Tool: Exit $rc, Output '$out'"; fi

# --- kaputtes JSON: fail-open mit Warnung auf stderr ---
errfile="$(mktemp)"
out="$(printf '{"tool_name": "Bash", kaputt' | "$BASH_BIN" "$HOOK" 2>"$errfile")"
rc=$?
err="$(cat "$errfile")"
if [ "$rc" -eq 0 ] && [ -z "$out" ] && [ -n "$err" ]; then ok; else bad "kaputtes JSON: Exit $rc, Output '$out', stderr '$err'"; fi

# --- jq fehlt: fail-open mit Warnung auf stderr ---
out="$(bash_json 'git add -A' | PATH="/nonexistent-git-guard-test" "$BASH_BIN" "$HOOK" 2>"$errfile")"
rc=$?
err="$(cat "$errfile")"
if [ "$rc" -eq 0 ] && [ -z "$out" ] && [ -n "$err" ]; then ok; else bad "jq fehlt: Exit $rc, Output '$out', stderr '$err'"; fi
rm -f "$errfile"

echo "git-guard.test.sh: $pass bestanden, $fail fehlgeschlagen"
[ "$fail" -eq 0 ]
