---
id: infrastructure-cbr0r
title: "Rules konsolidieren, Test-Doktrin anlegen, standards eindampfen, Review-Agents reparieren und kürzen"
status: done
type: refactor
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-ky2kh, infrastructure-vm8zz]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Rules veraltet oder auf falschen Pfaden; `localization.md` auskommentiert; drei Review-Agents laden nicht (YAML) und sind veraltet; `standards/backend/overview.md:22-23` beschreibt Giraffe Some/None irreführend (Audit A, B1, B2). Souls E4, E6, E7.

## What
Löschen: `build-verification`, `feature-workflow`, `localization`. `domain-purity`, `persistence-safety`, `query-safety` auf Resttext mit Pfaden `src/Server/**`. `testing.md` durch `test-doctrine.md` (Doktrin aus Bericht D und Souls 3.3, BudgetBuddy-Pflichtliste: Pipeline, Endpoint-Negativtests, Live nur hinter Schalter, Bot-Grenzen, keine Secrets an den Client). `standards/` eindampfen, `styleguide.md` bleibt. `backend-code-quality`, `frontend-architecture`, `design-quality` mit gültigem Frontmatter und aktuellem Inhalt; `qa-milestone-reviewer` zum Test-Reviewer gegen die Doktrin; `review-code.md` und `red-test-fixer` löschen.

## Acceptance criteria
- [ ] Alle Agents in `.claude/agents/` erscheinen in der Agent-Liste einer frischen Session.
- [ ] Kein Rule-`paths:` zeigt auf eine nicht existierende Datei (Test oder Skript prüft das).
- [ ] `standards/backend/overview.md` beschreibt `skipPipeline` korrekt.
- [ ] Keine DaisyUI-Klassen und keine Tailwind-3-Syntax in Agents oder standards (grep).

## Notes
Migrationsschritt M10. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Nach diesem Schritt messen. Abbruchkriterium: Referenz-Task verschlechtert sich, Schritt zurücknehmen.

## Outcome

Migrationsschritt M10 (Souls E4, E6, E7) ist umgesetzt. Das README_DELTA wurde mit `node scripts/readme-delta-check.mjs --bc infrastructure` geprüft (Exit 0).

**Rules (`.claude/rules/`):**
- Gelöscht: `build-verification.md`, `feature-workflow.md`, `localization.md` (war auskommentiert), `testing.md` (veraltet: Supabase, `RUN_SMOKE_TESTS`, `setup-env.sh`, `fix-tests`-Agent; ersetzt durch die Doktrin).
- `domain-purity.md`, `persistence-safety.md`, `query-safety.md`: `paths: src/Server/**`, je ein Satz "Mechanisch geprüft von" mit Verweis auf `src/Tests/StructureGuardTests.fs` (bei persistence-safety zusätzlich `TestDatabaseGuardTests.fs`, `ModuleLevelDbInitGuardTests.fs`), danach nur das Urteil, das kein Wächter prüft (synchrone Domain, N+1, Transaktion über mehrere Tabellen, kein stiller Default, neue `OptionHandler`-Registrierung, Grenzen des SQL-Wächters). Die alten Grep-Checks sind entfallen.
- `semantic-messaging.md`: Beispiele aus BudgetBuddy (`setEntryMemo`, `setReviewMark`, `setEntryCategoryBatch`, `restoreFlagBatch`, `retryPushToYnab`, `EntryObservedInYnab`, `getLedgerAccounts`, `getBudgetMonth`), Weg Api.fs, CommandHandlers, QueryHandlers im Kassenbuch. Alle Namen per grep geprüft.
- Neu: `test-doctrine.md` mit Heimat-Ebene (Bericht D Abschnitt 9, Souls 3.3), "Kein Test für", Bugfix-Regeln (Regressionstest rot vor dem Fix, kaputt- und intakt-Szenario), BudgetBuddy-Pflichtliste (Pipeline über `Server.Api.webApp()`, Endpoint-Negativtabelle, Live nur hinter `RUN_INTEGRATION_TESTS`, Bot-Grenzen, keine Secrets an den Client mit Verweis auf `infrastructure-s1r95`), Testdatenbank und Umgebung, Property-Tests ohne Pflicht, Stil. Gate `./verify.sh`. Gerettetes Wissen aus infrastructure-vm8zz Punkte 1 bis 4 ist in den Punkten 15, 8, 6 und 18 eingearbeitet; Bezeichner per grep geprüft (`USE_MEMORY_DB` in `Main.fs` und `test.runsettings`, `TZ=Europe/Berlin`, "Testlauf zeigt auf eine In-Memory-Datenbank", `testProperty` in `YnabClientTests.fs`, `MemoProbe`, `freshAccount`, `testSequenced`, `shouldRunIntegrationTests`).
- Tote Pfade ebenfalls behoben: `elmish-state.md` (`src/Client/State/**` wird `src/Client/Components/**/State.fs`), `fable-pitfalls.md` (`src/Tests/E2E/**` entfernt).

**Agents (`.claude/agents/`):** `review-code.md` (`code-reviewer`) und `red-test-fixer.md` gelöscht. `backend-code-quality`, `frontend-architecture`, `design-quality` neu und kurz (vorher 460 bis 540 Zeilen mit ungültigem Frontmatter, DaisyUI, Tailwind 3, `fsharp-*`-Skills, `reviews/`): einzeiliges Frontmatter mit `name`, `description`, `tools`, `model`, `color` nach dem Muster der agentheim-Agents; Inhalt nach den Souls-Vorbildern, Abschnitt "Was Wächter schon prüfen", Bericht als Antwort an den Aufrufer. `qa-milestone-reviewer` ist jetzt Test-Reviewer gegen `test-doctrine.md` (kein MILESTONE-PLAN, keine Übergabe an red-test-fixer). Name bleibt, weil CLAUDE.md ihn aufruft. Kein Agent schreibt Dateien, ins Diary oder nach `reviews/`.

**standards/ (E6), gelöscht oder eingedampft:**
- `README.md`: neu, kurzer Index der vier verbliebenen Dateien (vorher Blueprint-Navigation mit Skill-Verweisen).
- `backend/overview.md`: neu geschrieben. Aufbau des Servers, Giraffe-Pipeline: `Some` beendet die Anfrage, `next ctx` liefert am Ende ebenfalls `Some`, `None` heißt `skipPipeline`; im `choose` von `Server.Api.webApp` muss ein weitergebender Handler `skipPipeline` liefern (Bug 2a5c229). Dazu kurz Milliunits und `Encode.int`, `BUDGETBUDDY_ENCRYPTION_KEY`, kein Login außer Bot-Token, `DATA_DIR`.
- `frontend/overview.md`: neu geschrieben. Aufbau des Clients und Gelerntes (`PendingVersion`, Optionen vor der Schleife berechnen, `prop.key`, Doppelklick, kein Hover).
- `frontend/styleguide.md`: unverändert. Einziger Treffer im grep ist die Feststellung "kein DaisyUI, ADR 0031" in Zeile 170; der Wächter lässt genau diese Wendung zu.
- Gelöscht, weil generisches Blueprint-Material (Todo- und Item-Beispiele, aus dem Code ablesbar): `backend/api-implementation.md`, `backend/domain-logic.md`, `backend/error-handling.md`, `backend/persistence-sqlite.md`, `frontend/routing.md`, `frontend/view-patterns.md`, `frontend/state-management.md`, `frontend/remotedata.md` (Helfer stehen in CLAUDE.md und `Types.fs`), `shared/types.md`, `shared/api-contracts.md`, `shared/validation.md`, `global/quick-reference.md`, `global/architecture.md`, `global/development-workflow.md`.
- `backend/persistence-files.md`: gelöscht, JSON-Dateipersistenz gibt es in BudgetBuddy nicht.
- `testing/*.md` (sechs Dateien): gelöscht, ersetzt durch `.claude/rules/test-doctrine.md`; sie zeigten Muster wie `Api.itemApi`, die es nicht gibt.
- `deployment/*.md` (vier Dateien): gelöscht, beschrieben ein Tailscale-Sidecar-Setup, das nicht mehr gilt; `Dockerfile`, `compose.yaml` und `scripts/deploy.sh` dokumentieren sich selbst.
- `global/claude-tools.md`: gelöscht, schrieb Serena und die gelöschten Skills vor.
- `global/anti-patterns.md`, `global/learnings.md`: gelöscht; das Projektspezifische (Verschlüsselungsschlüssel aus der Umgebung, `Encode.int`, DB-Init beim Start, Versionierung, Performance) steht jetzt in den beiden overview-Dateien, der Rest war generisch. "SQLite im WAL-Modus" nicht übernommen, `journal_mode` kommt im Code nicht vor.

**CLAUDE.md:** nur "Before Implementing Anything", die Documentation Map, Schritt 2 unter "Implementing User Specifications" und die red-test-fixer-Zeile im QA-Abschnitt angepasst; kein Verweis zeigt mehr auf eine gelöschte Datei. Die Development Order und die Checkliste nennen weiter `src/Server/Domain.fs` ohne Backticks (gehört zu M12).

**Wächter (AC 2 und AC 4):** `scripts/claude-harness-check.mjs` mit `scripts/claude-harness-check.test.mjs` (9 Tests, in `npm run test:scripts`). Prüft Rule-`paths:` gegen echte Dateien, Agent-Frontmatter auf `name` und `description` und YAML-Fehler, Backtick-Pfade in Rules und Agents, `standards/`-Verweise in CLAUDE.md, DaisyUI- und Tailwind-3-Muster (`btn-`, `card-body`, `badge-`, `daisyui`, `@tailwind`, `tailwind.config`, `bg-opacity-`) in Agents und standards. Gegenbeweise mit Fixtures (toter Pfad, auskommentierte Rule, alte mehrzeilige Beschreibung, fehlender name, `: ` im Wert, toter Verweis). Zusätzlich live geprüft: altes `design-quality.md` und alte `domain-purity.md` zurückgelegt, Prüfung meldet 16 Funde; danach wiederhergestellt, sauber. Der Leser ist strenger als Claude Code: der alte `qa-milestone-reviewer` lud trotz `: ` im Wert, der Wächter hätte ihn gemeldet.

**AC 1:** eine frische Session kann ich nicht starten. Der Frontmatter-Test ist der mechanische Ersatz; die Agent-Liste muss der Conductor danach live prüfen (erwartet: backend-code-quality, design-quality, frontend-architecture, qa-milestone-reviewer; code-reviewer und red-test-fixer fehlen).

**Gate:** `./verify.sh` VERIFY: PASS (dotnet 2600 grün, 8 erlaubt übersprungen; node 319 grün, vorher 310, plus 9 neue; build, npm build, guard:classes, guard:tokens grün).

**Nachtrag Conductor:** Zwei Hinweise des Verifiers bei der Integration umgesetzt: `standards/frontend/overview.md` nennt `PendingCategoryVersions` statt des nicht existierenden `PendingVersion`; `.claude/rules/fable-pitfalls.md` hat wieder den Hinweis zu `sprintf "%.2f"` (Quelle: gelöschte `standards/global/learnings.md`).
