---
id: infrastructure-vm8zz
title: "Legacy löschen: fsharp-*-Skills, tailscale-deploy, web-animation-design, backlog.md, MILESTONE-PLAN, reviews, template, legacy, next-step, salvage"
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
Alle genannten Teile ohne Nutzung seit Monaten (Audit A Abschnitt 9). Souls E5.

## What
Vor dem Löschen nicht herleitbares Wissen aus `fsharp-*`, `tailscale-deploy` und `standards/` retten (Ziel: Test-Doktrin, BC-README, CLAUDE.md-Entwurf); jeder gerettete Bezeichner per grep gegen den Code geprüft.

## Acceptance criteria
- [ ] Die genannten Dateien und Ordner sind entfernt; `rules.yml` bleibt (Produktdaten).
- [ ] Eine Liste des geretteten Wissens mit Zielort liegt im Task-Outcome.
- [ ] Kein Verweis in CLAUDE.md, Rules, Agents oder Skripten zeigt auf Gelöschtes (grep).

## Notes
Migrationsschritt M9. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.


## Outcome

Migrationsschritt M9 (Souls E5) ist umgesetzt. Gelöscht wurden 69 getrackte Dateien: der ganze Ordner `.claude/skills/` (12 `fsharp-*`-Skills, `tailscale-deploy`, `web-animation-design`, `README.md`, das nur diese Skills indexierte), `backlog.md`, `docs/MILESTONE-PLAN.md`, `reviews/`, `template/` und `legacy/`. Im FILE_LIST stehen die vier gelöschten Ordner als Verzeichnispfade statt als 63 Einzeldateien; `git status` im Worktree zeigt die vollständige Liste (69 Zeilen ` D`). `rules.yml` bleibt erhalten. `legacy/` war weder in `BudgetBuddy.sln` noch in einer fsproj oder `vite.config.js` eingebunden.

**Vom Conductor auf main zu löschen (liegt unter `.agentheim/`, für den Worker gesperrt):** `.agentheim/next-step.md` (getrackt) und `.agentheim/salvage/` (gitignored). Die Zeile `.agentheim/salvage/` in `.gitignore` habe ich stehen lassen; sie kann nach dem Löschen entfallen.

**Bearbeitete Dateien (wichtig für die Merge-Reihenfolge mit M13):**
- `CLAUDE.md`: Abschnitt "Using Skills" (beide Skill-Tabellen) entfernt. Abschnitt "Milestone & Backlog Tracking" heißt jetzt "Qualitätssicherung"; entfernt sind die Bezüge auf `/docs/MILESTONE-PLAN.md` und `/backlog.md`, Punkt 5 "Backlog Update", die Unterabschnitte "Milestone-spezifisch" und "Backlog-spezifisch" und beide Beispiele. In der Verification Checklist ist der Unterpunkt "If working on a milestone" samt MILESTONE-PLAN-Zeile zu zwei normalen Punkten (QA-Reviewer, fehlende Tests) geworden. Diary-Abschnitt, Diary-Pflicht in den Qualitätskriterien und alle qa-milestone-reviewer-Nennungen sind unverändert.
- `README.md` (Projekt): MILESTONE-PLAN-Zeile im Strukturbaum, `legacy/`-Zeile, Doku-Tabellenzeile "Milestone Plan" und Abschnitt "Roadmap" entfernt.
- Keine Datei unter `.claude/rules/`, `.claude/commands/` oder `scripts/` angefasst; dort und in `package.json` gibt es keinen Verweis auf Gelöschtes (grep).

**Offene Verweise außerhalb meines Auftrags (grep, bewusst nicht angefasst):**
- `.claude/agents/` (Auftrag: nicht anfassen, M10): `red-test-fixer.md` Z. 89 bis 91 (fsharp-tests, fsharp-backend, fsharp-frontend); `design-quality.md`, `backend-code-quality.md`, `frontend-architecture.md` verlangen den Skill `fsharp-frontend` bzw. `fsharp-backend` und schreiben nach `reviews/*.md`; `qa-milestone-reviewer.md` Z. 25 liest `docs/MILESTONE-PLAN.md` ("if applicable"). Das Akzeptanzkriterium "kein Verweis in Agents" ist damit erst mit M10 erfüllt.
- `standards/README.md` und `standards/global/claude-tools.md` nennen die Skills (Kondensierung in M10).
- `docs/MIGRATION-PLAN-SKILLS.md` und `docs/LEARNINGS.md` nennen Skills bzw. MILESTONE-PLAN. In `README.md` verlinkt die Doku-Tabelle schon vorher nicht existierende Dateien (`docs/00-ARCHITECTURE.md`, `docs/07-BUILD-DEPLOY.md` u. a.).

**Gerettetes Wissen (Quellen: `fsharp-*`, `tailscale-deploy`, `standards/`).** Der Großteil der Skills war generisches Blueprint-Material (Todo-Beispiele, Muster aus dem Code ablesbar). Übrig bleibt:

1. Tests laufen nie gegen die echte Datenbank: `src/Tests/Main.fs` setzt `USE_MEMORY_DB=true` vor jeder Persistenz, zusätzlich `src/Tests/test.runsettings` (`<USE_MEMORY_DB>true</USE_MEMORY_DB>`); ohne das landet ein Testlauf unter `DATA_DIR` (Default `~/my_apps/budgetbuddy-dev/budgetbuddy.db`, ADR 0030). Der Wächter `src/Tests/TestDatabaseGuardTests.fs` ("Testlauf zeigt auf eine In-Memory-Datenbank") sichert das ab. Einzeltests nutzen `Data Source=:memory:` (grep: `LedgerTests.fs`, `LedgerPushFailureTests.fs`, `LedgerWithoutCategoryTests.fs`). Ziel: test-doctrine.
2. Jeder Bugfix bekommt einen Regressionstest, der vor dem Fix rot ist, mit Kommentar, welchen Bug er verhindert (Quelle fsharp-tests, CLAUDE.md "Bug Fix Protocol"). Ziel: test-doctrine.
3. Keine Tautologie-Tests: Verhalten prüfen, keine gerade gesetzten Felder zurücklesen (Quelle `standards/global/learnings.md` "Testing Insights"). Ziel: test-doctrine.
4. Property-Tests mit FsCheck über Expectos `testProperty` gibt es (grep: FsCheck in `src/Tests/Tests.fsproj`, `testProperty` nur in `src/Tests/YnabClientTests.fs`, z. B. "milliunits conversion roundtrip", "memo with reference respects 300 char limit"). Geeignet für Umrechnungen und Grenzen. Ziel: test-doctrine.
5. Validierung sammelt alle Fehler und liefert konkrete Meldungen statt beim ersten Fehler abzubrechen; validiert wird an der API-Grenze, nicht in der Domänenlogik (Quelle fsharp-validation). Ziel: CLAUDE.md.
6. Dapper und F#-Optionen: Row-Typen brauchen `[<CLIMutable>]`, Optionsfelder einen registrierten `OptionHandler<'T>` (grep: `OptionHandler` in `src/Server/Persistence.fs`, registriert für `string` und `DateTime`; `CLIMutable` in `src/Server/Persistence.fs`, `src/Server/Budgeting/Persistence.fs`, `src/Server/Ledger/Persistence.fs`). Ein neuer Optionstyp (z. B. `int option`) braucht eine eigene Registrierung. Ziel: CLAUDE.md.
7. Beträge sind YNAB-Milliunits als `int64` (Quelle fsharp-shared; grep `milliunit` in `src/Tests/YnabClientTests.fs`, `BudgetingTests.fs`, `SplitPushTests.fs`, `QuickAddTests.fs`). Ziel: BC-README ledger (falls dort noch nicht genannt).
8. Versionsbasiertes Entprellen im Frontend: eine Änderung wird nur übernommen, wenn ihre Version noch die neueste ist (Quelle `standards/global/learnings.md`; grep `PendingVersion` in `src/Client/Components/SyncFlow/State.fs`). Ziel: BC-README des SyncFlow-Kontexts (vermutlich import/sync).
9. Teure Berechnungen (z. B. Kategorie-Optionen) einmal vor der Transaktionsschleife berechnen, inaktive Zeilen (Skipped) als einfaches Textelement statt voller Select-Komponente rendern (Quelle `standards/global/learnings.md`, Performance). Nicht per Bezeichner prüfbar, daher nur als Hinweis. Ziel: CLAUDE.md.

**Nicht gerettet, weil veraltet:** `tailscale-deploy` beschreibt ein `docker-compose` mit Tailscale-Sidecar (`network_mode: service:tailscale`, `TS_SERVE_CONFIG`, Volume `budgetbuddy_app-data`). Nichts davon steht in `compose.yaml` (grep leer); dort gelten `./data:/app/data`, Port 5081, Tailscale-Serve auf dem Host und das externe Netz `budgetbuddy_default` für `bb-bot`. `compose.yaml` dokumentiert das selbst. Ebenso nicht übernommen: "SQLite im WAL-Modus" aus `learnings.md`, `journal_mode` kommt im Code nicht vor.

**Gate:** `dotnet build` 0 Fehler; `dotnet test` 2502 bestanden, 6 übersprungen, 0 fehlgeschlagen; `npm run build` Exit 0; `guard:classes` und `guard:tokens` sauber; `npm run test:scripts` 285/285 bestanden (T8 prüft weiter die README-Delta-Regel in CLAUDE.md, die unverändert ist).

**Nachtrag Conductor (Integration):** `.agentheim/next-step.md` per `git rm` entfernt (veralteter Stand nach Quick-Add). `.agentheim/salvage/` gelöscht (sechs Patches zu `ledger-cks06`, `ledger-fa1fp`, `ledger-h6ptn`, `ledger-j8xq4`, `ledger-saqsk`, `ynab-sync-aj5tv`, alle inzwischen in `done/`). Die Zeile `.agentheim/salvage/` in `.gitignore` bleibt, weil agentheim dort bei Abbrüchen neue Patches ablegt. `.claude/agents/` und `standards/` waren per Dispatch aus M9 herausgenommen, ihre Verweise auf Gelöschtes bereinigt M10.
