#!/usr/bin/env bash
# git-Wächter (PreToolUse-Hook für das Bash-Tool).
#
# Liest das Hook-JSON von stdin, zerlegt den Bash-Befehl an &&, ||, ;, |, & und
# Zeilenumbrüchen und prüft jedes Teilstück, das ein git-Aufruf ist:
#   - deny: Sammel-Staging (add -A/--all/./:/, add -u ohne Pfad) und commit -a/--all
#   - ask:  jedes push
#   - sonst: kein Output, Exit 0 (die normalen Permissions entscheiden)
# Permission-Regeln vergleichen nur das Präfix; dieser Hook sieht auch
# `git -C <pfad> add -A` und Ketten wie `cd x && git add .`.
#
# Fail-open: Fehlt jq oder ist das Eingabe-JSON kaputt, gibt der Hook nur eine
# Warnung auf stderr aus und endet mit Exit 0, damit ein Defekt nicht jeden
# Bash-Aufruf sperrt. Der Hook nutzt außer jq nur bash-Builtins.

warn() { printf 'git-guard: %s\n' "$*" >&2; }

if ! command -v jq >/dev/null 2>&1; then
  warn "jq nicht gefunden, git-Wächter inaktiv (keine Entscheidung)"
  exit 0
fi

input=""
IFS= read -r -d '' input || true

if ! parsed="$(printf '%s' "$input" | jq -er '[.tool_name // "", (.tool_input.command // "")] | @json' 2>/dev/null)"; then
  warn "Eingabe-JSON nicht lesbar, git-Wächter inaktiv (keine Entscheidung)"
  exit 0
fi
tool_name="$(printf '%s' "$parsed" | jq -r '.[0]')"
command_text="$(printf '%s' "$parsed" | jq -r '.[1]')"

[ "$tool_name" = "Bash" ] || exit 0
[ -n "$command_text" ] || exit 0

SEP=$'\x1f'

# Zerlegt $1 in Wörter (Quotes werden aufgelöst) und Trenner ($SEP) nach TOKENS.
# Bewusst einfach: keine Subshell- oder Heredoc-Auswertung.
tokenize() {
  local s="$1" i=0 n=${#1} c next cur="" have=0 quote=""
  TOKENS=()
  while [ "$i" -lt "$n" ]; do
    c="${s:i:1}"
    if [ -n "$quote" ]; then
      if [ "$c" = "$quote" ]; then
        quote=""
      elif [ "$quote" = '"' ] && [ "$c" = '\' ] && [ $((i + 1)) -lt "$n" ]; then
        i=$((i + 1)); cur+="${s:i:1}"
      else
        cur+="$c"
      fi
      i=$((i + 1)); continue
    fi
    case "$c" in
      "'"|'"') quote="$c"; have=1 ;;
      '\')
        if [ $((i + 1)) -lt "$n" ]; then i=$((i + 1)); cur+="${s:i:1}"; have=1; fi ;;
      ' '|$'\t')
        if [ "$have" -eq 1 ]; then TOKENS+=("$cur"); cur=""; have=0; fi ;;
      ';'|'|'|'&'|$'\n'|'('|')')
        if [ "$have" -eq 1 ]; then TOKENS+=("$cur"); cur=""; have=0; fi
        next="${s:i+1:1}"
        if { [ "$c" = '|' ] || [ "$c" = '&' ]; } && [ "$next" = "$c" ]; then i=$((i + 1)); fi
        TOKENS+=("$SEP") ;;
      *) cur+="$c"; have=1 ;;
    esac
    i=$((i + 1))
  done
  if [ "$have" -eq 1 ]; then TOKENS+=("$cur"); fi
}

deny_reasons=()
ask_reasons=()

is_all_pathspec() {
  case "$1" in
    .|./|:|:/|:/.|':(top)'|'*') return 0 ;;
  esac
  return 1
}

# Prüft `git add` mit den Argumenten nach dem Unterbefehl.
check_add() {
  local arg after_dd=0 update=0 all=0 paths=0 all_path=0 flags
  for arg in "$@"; do
    if [ "$after_dd" -eq 1 ]; then
      paths=$((paths + 1)); is_all_pathspec "$arg" && all_path=1; continue
    fi
    case "$arg" in
      --) after_dd=1 ;;
      --all|--no-ignore-removal) all=1 ;;
      --update) update=1 ;;
      --*) ;;
      -?*)
        flags="${arg#-}"
        case "$flags" in *A*) all=1 ;; esac
        case "$flags" in *u*) update=1 ;; esac ;;
      *) paths=$((paths + 1)); is_all_pathspec "$arg" && all_path=1 ;;
    esac
  done
  if [ "$all" -eq 1 ] || [ "$all_path" -eq 1 ]; then
    deny_reasons+=("git add stagt hier alles (-A, --all, . oder :/). Explizite Pfade stagen (scoped commit), damit fremde Änderungen anderer Worktrees oder des Owners nicht mitrutschen.")
  elif [ "$update" -eq 1 ] && [ "$paths" -eq 0 ]; then
    deny_reasons+=("git add -u ohne Pfad stagt alle geänderten Dateien. Explizite Pfade stagen (scoped commit).")
  fi
}

# Prüft `git commit`: -a, --all und Kurzoptionsbündel mit a (-am, -av).
check_commit() {
  local skip=0 arg flags j ch
  for arg in "$@"; do
    if [ "$skip" -eq 1 ]; then skip=0; continue; fi
    case "$arg" in
      --) break ;;
      --all) deny_reasons+=("git commit --all nimmt alle geänderten Dateien mit. Explizite Pfade stagen, dann committen (scoped commit)."); return ;;
      --message|--file|--author|--date|--template|--reuse-message|--reedit-message|--fixup|--squash|--trailer|--cleanup|--pathspec-from-file)
        skip=1 ;;
      --*) ;;
      -?*)
        flags="${arg#-}"
        for ((j = 0; j < ${#flags}; j++)); do
          ch="${flags:j:1}"
          case "$ch" in
            a) deny_reasons+=("git commit -a nimmt alle geänderten Dateien mit. Explizite Pfade stagen, dann committen (scoped commit)."); return ;;
            m|F|C|c|t)
              # Rest des Bündels ist der Wert; sonst folgt er als nächstes Wort.
              [ $((j + 1)) -ge ${#flags} ] && skip=1
              break ;;
          esac
        done ;;
    esac
  done
}

# Prüft ein Teilstück (Wörter ohne Trenner).
check_segment() {
  local -a w=("$@")
  local i=0 n=${#w[@]}
  # Umgebungszuweisungen vor dem Befehl überspringen (FOO=1 git ...).
  while [ "$i" -lt "$n" ] && [[ "${w[i]}" =~ ^[A-Za-z_][A-Za-z0-9_]*= ]]; do i=$((i + 1)); done
  [ "$i" -lt "$n" ] || return 0
  case "${w[i]}" in
    git|*/git) ;;
    *) return 0 ;;
  esac
  i=$((i + 1))
  # Globale git-Optionen vor dem Unterbefehl überspringen.
  while [ "$i" -lt "$n" ]; do
    case "${w[i]}" in
      -C|-c|--git-dir|--work-tree|--namespace|--exec-path|--config-env|--super-prefix) i=$((i + 2)) ;;
      -*) i=$((i + 1)) ;;
      *) break ;;
    esac
  done
  [ "$i" -lt "$n" ] || return 0
  local sub="${w[i]}"
  local -a rest=("${w[@]:i+1}")
  case "$sub" in
    add) check_add ${rest[@]+"${rest[@]}"} ;;
    commit) check_commit ${rest[@]+"${rest[@]}"} ;;
    push) ask_reasons+=("git push nur nach ausdrücklicher Freigabe des Owners (Push löst auf dem Remote den Deploy aus).") ;;
  esac
}

tokenize "$command_text"
segment=()
for tok in ${TOKENS[@]+"${TOKENS[@]}"} "$SEP"; do
  if [ "$tok" = "$SEP" ]; then
    if [ "${#segment[@]}" -gt 0 ]; then check_segment "${segment[@]}"; fi
    segment=()
  else
    segment+=("$tok")
  fi
done

emit() {
  jq -cn --arg d "$1" --arg r "$2" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:$d,permissionDecisionReason:$r}}'
}

if [ "${#deny_reasons[@]}" -gt 0 ]; then
  emit deny "${deny_reasons[0]}"
elif [ "${#ask_reasons[@]}" -gt 0 ]; then
  emit ask "${ask_reasons[0]}"
fi
exit 0
