---
id: infrastructure-jcb2m
title: "Worktree-Skripte: node_modules-Symlink anlegen und vor git worktree remove wieder löschen"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Lehre aus Souls und Memory: Worker brauchen node_modules im Worktree; Teardown mit Symlink scheitert oder löscht zu viel.

## What
`scripts/worktree-prep.sh` und `scripts/worktree-teardown.sh` nach Souls-Vorlage.

## Acceptance criteria
- [ ] Prep legt den Symlink an, `./verify.sh` läuft danach im Worktree.
- [ ] Teardown löscht den Symlink vor `git worktree remove`, Schritte nicht per `&&` verkettet; das Haupt-`node_modules` bleibt erhalten.

## Notes
Migrationsschritt M4. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.


## Outcome

Zwei Skripte nach Souls-Vorlage, an BudgetBuddy angepasst:

- `scripts/worktree-prep.sh <worktree> [--dry-run]`: legt den Worktree nicht selbst an. Setzt den Symlink `node_modules` auf das `node_modules` des Haupt-Repos (übersprungen, wenn schon vorhanden; Abbruch, wenn dort ein echter Ordner liegt) und kopiert `.env`. Die Souls-Schritte `supabase/.temp`, `npm run predev` und `npm run build` entfallen, weil BudgetBuddy kein Supabase hat und `./verify.sh` selbst baut.
- `scripts/worktree-teardown.sh <worktree> [--delete-branch] [--dry-run]`: nur direkte Unterordner von `<Haupt-Repo>/.worktrees/` (kein `..`, kein Punkt-Ordner, nicht das Haupt-Repo). Schritte einzeln: (1) jeden Symlink löschen und prüfen, dass keiner übrig ist; (2) `git worktree remove --force` nur nach (1), danach Reste: ist der Ordner nicht mehr bei git eingetragen, ohne Symlink und nur mit Dateien unter `obj/` oder `bin/`, wird er gelöscht, sonst bleibt er stehen und der Exit-Code ist 1; (3) `git branch -D aw/<name>` nur mit `--delete-branch` (Abweichung von Souls: ohne Option bleibt der Branch, weil er noch nicht integrierte Arbeit tragen kann); (4) `git worktree prune` immer.
- `scripts/baseline-replay.sh`: prepare und teardown rufen jetzt die beiden Skripte statt der Inline-Logik; CLI, replay-*-Prüfung und Ausgabe bleiben gleich. Kopfkommentar angepasst. Probelauf `prepare infrastructure-dvy29` und `teardown` grün.
- Tests: `scripts/worktree-scripts.test.mjs` (6 Tests, läuft in `npm run test:scripts`): Rundlauf prep/teardown an einem Wegwerf-Worktree (Symlink-Ziel, `.env`, Symlink-Schritt vor remove, Ordner und Eintrag weg, Haupt-`node_modules`-Anzahl unverändert, Branch bleibt), `--delete-branch`, Verweigerung von Pfaden außerhalb von `.worktrees/` (Zielordner mit Symlink bleibt unberührt), Löschen eines reinen obj/bin-Rests, Stehenlassen eines Rests mit fremder Datei, Abbruch von prep bei echtem `node_modules`-Ordner. Gegenprobe: ohne die Rest-Behandlung wird der obj-Test rot.

AC 1 belegt durch einen eigenen Lauf: Wegwerf-Worktree `.worktrees/jcb2m-ac1` (Branch `aw-test/jcb2m-ac1` an HEAD) angelegt, nur mit `worktree-prep.sh` vorbereitet, dort `./verify.sh`: `VERIFY: PASS (dotnet 2502 grün, node 300 grün, ...)`. Danach mit `worktree-teardown.sh` abgebaut. Sekunden später legte der Sprachserver `src/Server/obj/...` in den entfernten Ordner neu an; ein zweiter Aufruf von `worktree-teardown.sh .worktrees/jcb2m-ac1` erkannte den Rest als nicht eingetragen und reines obj/ und löschte ihn. Ein einzelner Lauf fängt Stubs, die erst nach seinem Ende entstehen, nicht ab. Haupt-`node_modules` blieb bei 401 Einträgen. Gate im eigenen Worktree: `VERIFY: PASS (dotnet 2502 grün, node 306 grün, ...)`. Alle Wegwerf-Worktrees und aw-test/-Branches sind entfernt.

Diary (statt diary/development.md, wegen paralleler Worker): 2026-10-29, Dateien neu: scripts/worktree-prep.sh, scripts/worktree-teardown.sh, scripts/worktree-scripts.test.mjs; geändert: scripts/baseline-replay.sh. Build und Gate grün.
