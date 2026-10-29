---
id: infrastructure-dvy29
title: "Ein Gate-Kommando verify.sh, Test command in allen BC-READMEs und git-guard als PreToolUse-Hook"
status: done
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

## Outcome

`./verify.sh` im Repo-Wurzelverzeichnis ist das Gate-Kommando. Es läuft immer alle sechs Schritte (`dotnet build BudgetBuddy.sln`, `dotnet test src/Tests/Tests.fsproj` mit trx-Datei, `npm run build`, `npm run guard:classes`, `npm run guard:tokens`, `npm run test:scripts`) und endet mit genau einer Zeile `VERIFY: PASS (...)` oder `VERIFY: FAIL (rote Schritte: ...)`. Bei FAIL folgen die Namen der roten Tests (Expecto aus der trx-Datei samt erster Fehlerzeile, node --test aus dem Abschnitt "failing tests" samt Datei:Zeile) und der Pfad der aufbewahrten Logs; Exit 1. Übersprungene Pflichttests sind rot. Erlaubt sind nur die opt-in-Integrationstests gegen echte YNAB- und Comdirect-APIs (`testList "... (requires .env)"`, heute 6). Unlesbare Zusammenfassungen und 0 gelaufene Tests zählen als rot. MSB3030 im Build bekommt einen Hinweis auf parallel laufendes `dotnet watch`.

Ursache des Parallel-Flakes `export-design-system.test.mjs` mit `ERR_CONNECTION_REFUSED`: `startPreviewServer` in `scripts/export-design-system.mjs` wartet auf `vite preview` höchstens fest 5 Sekunden und macht danach ohne Erreichbarkeitsprüfung weiter. Unter der Last mehrerer gleichzeitiger Läufe ist der Server dann noch nicht da. (`screenshot-route.mjs` prüft dagegen per echtem TCP-Connect.) Deshalb ist nur `test:scripts` serialisiert: mkdir-Lock unter `$(git rev-parse --git-common-dir)/bb-verify-test-scripts.lock`, für Haupt-Checkout und alle Worktrees derselbe Pfad. Das Lock notiert pid, Startzeit (ps lstart, gegen wiederverwendete PIDs) und cwd; ein Lock eines toten Prozesses oder einer wiederverwendeten PID wird atomar übernommen; Höchstwartezeit 3600 s (`VERIFY_LOCK_MAX_WAIT_SECONDS`). Expecto, Builds und Wächter laufen parallel ohne Lock. Den eigentlichen Timeout-Fehler beschreibt ein Backlog-Item.

Nachweis AC 4: zwei gleichzeitige `./verify.sh`-Läufe, einer im Worktree, einer in einer per rsync angelegten Wegwerf-Kopie im Scratchpad (deren `.git`-Datei zeigt auf dasselbe gemeinsame git-Verzeichnis, kein git-Befehl). Beide `VERIFY: PASS` (dotnet 2502 grün, node 300 grün), der zweite wartete sichtbar auf das Lock des ersten, das Lock war danach entfernt. Rot-Nachweis in einer weiteren Kopie mit absichtlich rotem und übersprungenem Test je Runner: `VERIFY: FAIL (rote Schritte: dotnet test, test:scripts)` mit allen vier Namen, Exit 1. Ein Lauf dauert rund 4,5 Minuten, zwei parallel rund 7 Minuten.

git-Wächter: `.claude/hooks/git-guard.sh` (nach Souls-Vorlage, Begründungstexte auf BudgetBuddy angepasst: scoped commit, push löst über den post-receive-Hook den Deploy aus) ist als PreToolUse-Hook mit Matcher `Bash` in `.claude/settings.json` eingetragen, der `env`-Block bleibt. deny: `git add -A/--all/./:/`, `git add -u` ohne Pfad, `git commit -a/-am/--all`, auch mit `git -C <pfad>`, globalen Optionen und in Ketten. ask: jedes `git push`. Explizite Pfade und `git commit -m` laufen durch. `.claude/hooks/git-guard.test.sh` hat 58 Fälle, alle grün; `scripts/git-guard.test.mjs` hängt ihn an `npm run test:scripts`. Beide .sh-Dateien sind ausführbar (Modus beim Commit erhalten). Hinweis für den Owner: `.claude/settings.local.json` erlaubt `Bash(git push)` pauschal. Das ask des Hooks greift trotzdem, der Owner kann den Eintrag streichen. settings.local.json wurde nicht angefasst.

Für alle sieben BC-READMEs (README_DELTA kann keine neuen Abschnitte anlegen, der Conductor setzt ihn von Hand) gilt dieser Abschnitt:

```
## Test command

`./verify.sh` im Repo-Wurzelverzeichnis. Läuft dotnet build, dotnet test, npm run build, guard:classes, guard:tokens und test:scripts; grün nur bei der Schlusszeile `VERIFY: PASS` und Exit 0. Bei `VERIFY: FAIL` stehen die roten Schritte und die Namen der roten Tests am Ende der Ausgabe.
```

Dateien: `verify.sh`, `scripts/verify-lib.sh` (Parser und Lock, bash 3.2), `scripts/verify-lib.test.mjs` (14 Tests), `scripts/git-guard.test.mjs`, `.claude/hooks/git-guard.sh`, `.claude/hooks/git-guard.test.sh`, `.claude/settings.json`.

Diary (parallele Worker schreiben es hier statt in `diary/development.md`): 2026-10-29, verify.sh, verify-lib.sh mit Tests, git-guard-Hook mit Selbsttest und settings.json-Eintrag angelegt. Build grün, dotnet test 2502 grün (6 opt-in übersprungen), test:scripts 300/300 grün, git-guard.test.sh 58/58.
