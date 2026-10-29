---
id: infrastructure-dvy29
title: "Ein Gate-Kommando verify.sh, Test command in allen BC-READMEs und git-guard als PreToolUse-Hook"
status: todo
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-psdcb]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Es gibt kein verify.sh. Verifier bekamen 38-mal nur `dotnet test`, 17-mal die volle Kette (Transkripte). Kein BC-README hat `## Test command`. Keine Hooks (Audit A, Souls E3).

## What
`verify.sh`: `dotnet build`, `dotnet test`, `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts`; Namen roter Tests am Ende; Exit-Code ungleich 0 bei Fehler oder übersprungenen Pflichttests; Lock nur, wenn `test:scripts` nicht parallel laufen kann. `## Test command` mit `./verify.sh` in allen sieben BC-READMEs. `.claude/hooks/git-guard.sh` plus `git-guard.test.sh` nach Souls-Vorlage, als PreToolUse-Hook in `.claude/settings.json`.

## Acceptance criteria
- [ ] `./verify.sh` endet mit eindeutiger Erfolgs- oder Fehlermeldung und listet rote Tests mit Namen.
- [ ] Alle sieben BC-READMEs nennen `./verify.sh` unter `## Test command` (README_DELTA, Conductor wendet an).
- [ ] `git-guard.test.sh` ist grün: `git add -A`, `git add .`, `git commit -a` werden abgelehnt, `git push` fragt nach, auch mit `git -C`.
- [ ] Zwei gleichzeitige `./verify.sh`-Läufe in zwei Worktrees stören sich nicht.

## Notes
Migrationsschritt M3. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Lehre aus Souls: Lock und rote Testnamen früh einbauen.
