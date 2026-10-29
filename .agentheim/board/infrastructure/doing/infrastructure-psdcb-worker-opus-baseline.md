---
id: infrastructure-psdcb
title: "Subagents auf Opus erzwingen und Baseline-Replay der drei Referenz-Tasks mit dem heutigen Harness"
status: doing
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
77 Worker-Dispatches liefen ohne `model` auf Sonnet (Bericht A). Vor jeder Harness-Änderung braucht es eine Baseline, sonst ist keine Verschlankung messbar (Souls E1, E12).

## What
`.claude/settings.json` mit env `CLAUDE_CODE_SUBAGENT_MODEL=opus` und `CLAUDE_CODE_SUBAGENT_MODEL_FORCE=1`. `scripts/baseline-replay.sh` nach Vorlage `souls/scripts/baseline-replay.sh`, angepasst an BudgetBuddy. Baseline der Referenz-Tasks `ledger-ssmp3`, `design-system-j6dnj`, `ledger-ybm5x` im Zustand vor `work` auf dem Eltern-Commit, Ergebnis verworfen.

## Acceptance criteria
- [ ] `.claude/settings.json` existiert und enthält den env-Block; ein neu gestarteter Subagent meldet `claude-opus-*` im Transkript.
- [ ] `scripts/baseline-replay.sh <task-id>` legt einen Wegwerf-Worktree auf dem Eltern-Commit an und räumt ihn danach wieder ab (Symlinks zuerst).
- [ ] Für jede der drei Referenz-Tasks liegt ein Messprotokoll vor: Verifier-Verdict und Iterationen, Gate-Ergebnis, Input- und Output-Tokens, Laufzeit.

## Notes
Migrationsschritt M0. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Die Baseline-Läufe startet der Conductor; das Skript selbst kann ein Worker bauen, falls der Task gesplittet wird.

Stand 2026-10-29 (Conductor): `.claude/settings.json` mit Opus-env und `scripts/baseline-replay.sh` sind angelegt und per `--dry-run` geprüft (ledger-ssmp3: f1862dd -> cfb05ff, design-system-j6dnj: 7b82ca8 -> 897b13c, ledger-ybm5x: db6edad -> a0a8cab; unbekannte Id und Pfad außerhalb `replay-*` brechen mit Exit 1 ab). Offen: die drei Baseline-Läufe. Die env wirkt erst in einer neuen Claude-Code-Session.

### Ablauf der Baseline-Läufe (nach Souls infrastructure-j3208, angepasst)
Läuft in einer neuen Session (env greift beim Start), in der Hauptsession durch den Conductor, nicht über `/agentheim:work`. Je Referenz-Task nacheinander:
1. `scripts/baseline-replay.sh prepare <task-id>`.
2. Worker (`agentheim:worker`) mit dem Dispatch-Prompt des work-Skills, vollständiges Rückgabeformat aus `references/worker-return-format.md` inline, Task-Datei und Workspace aus dem Replay-Worktree. Danach Verifier (Opus) mit dem Gate `dotnet build && dotnet test && npm run build && npm run guard:classes && npm run guard:tokens && npm run test:scripts` (bis `verify.sh` aus M3 existiert). Bei FAIL bis zu zwei Re-Dispatches; Iterationen sind Messwert.
3. Messen: Verdict, Iterationen, Gate-Ergebnis, Dauer Dispatch bis Verdict, Subagent-Tokens, Worker-Modell aus dem Transkript.
4. Qualität: `code-reviewer` auf beide Diffs (Nachspiel und echter Ergebnis-Commit), Befunde nach Schwere zählen (bis M10 die reparierten Review-Agents liefert). Blinder Paarvergleich: ein Opus-Agent (general-purpose) bekommt beide Diffs als A und B in zufälliger Reihenfolge, ohne Herkunft.
5. `scripts/baseline-replay.sh teardown <worktree>`.

Rubrik (je Achse 1 bis 5 plus ein Satz, dann A besser / gleich / B besser):
- Semantik: Commands/Events, pure Domain, Geschäftsregeln am richtigen Ort.
- F#-Idiomatik: Typen statt Strings, DU statt widersprüchlicher Records, keine match-Pyramiden, keine Duplikation.
- Security: Bot-Grenzen (Token, Drossel, Beträge, Datumsfenster), keine Secrets an Client oder Log, Live-Dienste nur hinter Schalter.
- Tests: Verhaltensaussagen, keine Tautologien, richtige Ebene, AC-Abdeckung.
- Erfüllung der ACs.

Ergebnis nach `.agentheim/knowledge/research/harness-audit/baseline.md`: je Task Verdict, Iterationen, Gate, Dauer, Tokens, Worker-Modell, Review-Befunde beider Diffs, Paarurteil; Ablauf und Rubrik wörtlich; Grenzen (ein Lauf pro Task, Streuung unbekannt). Danach kein `.worktrees/replay-*` mehr, `ls node_modules | wc -l` unverändert.
Vergleichswerte des echten Laufs aus `.agentheim/board/protocol.md` (Dauer, Iterationen); echte Worker liefen auf Sonnet.

Stand 2026-10-29 17:40 (Conductor): Baseline gelaufen, Ergebnis in `.agentheim/knowledge/research/harness-audit/baseline.md`. Alle drei Referenz-Tasks PASS in Iteration 1, Worker laut Transkript `claude-opus-5-5`, blinder Paarvergleich 3 zu 0 für das Opus-Nachspiel. Replay-Worktrees abgeräumt, `node_modules` unverändert (401). Alle ACs erfüllt; die Task kann nach Durchsicht durch den Owner als erledigt geschlossen werden.
