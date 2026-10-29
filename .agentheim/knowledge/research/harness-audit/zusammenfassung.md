# Harness-Audit BudgetBuddy: Zusammenfassung (Phase 2)

Stand: 2026-10-29. Erhebung, keine Änderungen an Harness oder Code, kein Commit.
Grundlage: Berichte `a-inventar-nutzung.md`, `b1-review-mit-harness-code-reviewer.md`,
`b2-review-mit-harness-projekt-reviewer.md`, `c-review-ohne-harness.md`, `d-test-qualitaet.md`
im selben Ordner. Mit **[geprüft]** markiert ist, was der Koordinator selbst im Code oder in den
Transkripten nachvollzogen hat.

Methodische Einschränkung: Alle Subagents bekommen CLAUDE.md automatisch. Das Review „ohne Harness“
(C) ist deshalb nicht harness-frei; C hat den Inhalt bewusst ignoriert und das vermerkt.

## 1. Produktfehler und Sicherheitsbefunde (nicht gefixt)

| # | Befund | Fundstelle | Quelle | Status |
|---|---|---|---|---|
| P1 | `BotApi.handler` steht als erste Alternative in `choose` und ruft für fremde Pfade `next ctx` statt `skipPipeline`. Nach Giraffe-Semantik endet `choose` damit, die Remoting-Handler laufen nicht: `/api/*` antwortet 200 mit leerem Rumpf. B2 hat das lokal mit frischem Build reproduziert (main: 0 Byte, `b4b5cc3^`: JSON). | `src/Server/BotApi.fs:401-402`, `src/Server/Api.fs:1987` | B2 | **Bestätigt und behoben.** Owner bestätigt den Ausfall in Produktion; Fix 2a5c229 (`skipPipeline`, Regressionstest mit echtem `choose`), Hotfix-Nachtrag 3f5c18f, in Produktion bestätigt e8b81bf (alle 2026-10-29). Die Live-Messung mit 56 Byte war damit kein Gegenbeleg (Inhalt nicht gelesen). |
| P2 | Drossel vor der Anmeldung mit einem globalen Zähler: 10 Anfragen pro Minute ohne Token sperren Bob aus. Laut ADR 0068 gewollt, Zielkonflikt nicht benannt. | `src/Server/BotApi.fs:404`, `:488` | B1, B2, C | [geprüft] |
| P3 | Ungültiger `BOT_MAX_AMOUNT_EUR` (z.B. „50,00“) fällt auf die Vorgabe 500 EUR zurück, die gleich dem harten Deckel ist. Ein Tippfehler beim Absenken öffnet das Maximum. | `src/Server/ServerConfig.fs:29,38,56` | C (B1 nur als Hinweis) | [geprüft] |
| P4 | Scheitert der YNAB-Push, steht die Buchung im Kassenbuch, die Antwort meldet `entry_id: null`. Bob legt neu an, Doppelbuchung. Widerspricht `docs/bot-api.md:39`. | `src/Server/BotApi.fs:334-339`, `src/Server/Ledger/CommandHandlers.fs:684-717` | B1, B2, C | nicht selbst geprüft, drei Agents übereinstimmend |
| P5 | Commit mit Ausnahme bleibt als fehlerhafter Task gespeichert, weitere Versuche liefern bis zum Purge 500. | `src/Server/BotApi.fs:76-78`, `:362-374` | B1, C (Verdacht) | nicht selbst geprüft |
| P6 | `ComdirectIntegrationTests` „handles invalid credentials gracefully“ prüft `RUN_INTEGRATION_TESTS` nicht: jeder `dotnet test` mit `.env` macht einen echten OAuth-Versuch bei Comdirect mit falschen Zugangsdaten. Ein YNAB-Test ruft die echte API mit Fantasie-Token. | `src/Tests/ComdirectIntegrationTests.fs:111`, `src/Tests/YnabIntegrationTests.fs:177` | D | [geprüft] (Comdirect) |
| P7 | `getSettings` liefert YNAB-Token, Comdirect-Secret und Passwort im Klartext an den Client; `/api/*` hat keine Authentifizierung (Schutz nur über Tailscale/Caddy). | `src/Server/Api.fs:145-186` | D | [geprüft] |
| P8 | Weitere Verdachtsfälle ohne eigene Prüfung: kein CSRF-Schutz und keine AllowedHosts auf `/api` (C, D); Remoting-Fehlerhandler reicht Ausnahmen durch, `importRules` umgeht die Regelvalidierung, Benutzer-Regex ohne `MatchTimeout`, `PageSize` ohne Obergrenze, Verschlüsselungsschlüssel fällt auf Maschinennamen zurück (D); SQL-Literale `'Bob'`/`'CashExpenseAdded'` lassen die Tagesgrenze bei Umbenennung still auf null fallen (B2). | diverse | C, D, B2 | Verdacht |

## 2. Wert der Regeln: B gegen C

- Alle Befunde mit Produktwirkung (P2, P4, P5) fanden B1 und C beide; B1 markiert jeden selbst als „ohne Regelbezug“. B1: die echten Befunde kamen überwiegend aus `docs/bot-api.md` und dem Ledger-README, nicht aus Regeln.
- P1 fand nur B2, über das Durchspielen der Giraffe-Pipeline, ebenfalls ohne Regelbezug. Eine Standards-Datei (`standards/backend/overview.md:22-23`) beschreibt die Some/None-Semantik irreführend und hat den Fehler eher begünstigt.
- Nur mit Harness gefunden: Schichtung (Geschäftsregeln in `Validation.fs`, CLAUDE.md:228-230, `standards/backend/overview.md:40-42`), Styleguide-Details (Rohklasse statt `TouchTargets.minHeight`, fehlendes `touch-action`, `standards/frontend/styleguide.md`), Funktionslänge (30-Zeilen-Grenze, von B1 selbst als zu absolut bewertet).
- Nur ohne Harness gefunden: P3 als Fehlerfall, CSRF/DNS-Rebinding-Verdacht, `BotCommitResult` als DU statt Record mit widersprüchlichen Zuständen.
- Lesart: Die Regeln tragen bei Stil- und Strukturfragen, die Fachdoku (`docs/`, BC-README) bei Semantik. Für Security brachte das regelfreie Review mehr. Die Stichprobe ist klein (8 Commits, Schwerpunkt Bot-API).

## 3. Harness-Befunde

**Always-on-Kontext** (A): etwa 16k Tokens in der Hauptsession, mit den Pfad-Rules, die bei jeder Arbeit in `src/` laden, rund 20k. Größter Posten ist die Projekt-CLAUDE.md (29 KB, etwa 7,3k Tokens), davon etwa 2,5k Design-System-Referenz (Z. 527 bis 909) und etwa 1,6k Milestone-, Diary- und QA-Pflichten. Worker in Worktrees laden CLAUDE.md und Rules zum Teil doppelt (28 von 78 Worker-Transkripten).

**Pflichten, die nicht mehr gelebt werden** (A, [geprüft] für qa-milestone-reviewer und Worker-Modell):
- `qa-milestone-reviewer` „nach jeder Implementierung“: 0 Dispatches. Faktisch übernimmt der agentheim-Verifier (87 Läufe).
- `backlog.md` zuletzt 2026-06-11, `docs/MILESTONE-PLAN.md` letzte Completion 2025-12-06. Das Board ist `.agentheim/board/`.
- Diary wird befolgt (434 KB), dieselbe Information steht in `protocol.md`, Task-Outcome und Commit-Message.
- Serena-Pflicht (CLAUDE.md:1-27): Subagents 0 Aufrufe, 2.036 Bash-Zugriffe auf `.fs`. `search_for_pattern`, `find_file`, `list_dir` existieren im Serena-Kontext `claude-code` nicht [geprüft: Aufruf schlug fehl]. `.claude/rules/serena-tools.md` widerspricht CLAUDE.md.
- Die 13 `fsharp-*`- und `tailscale`-Skills: 0 Aufrufe in Transkripten und `history.jsonl`.

**Defekte und veraltete Teile:**
- Drei Review-Agents (`backend-code-quality`, `design-quality`, `frontend-architecture`) laden nicht (ungültiges YAML in der mehrzeiligen description). Inhaltlich veraltet: DaisyUI-Klassen, falsche Schriften, Tailwind-3-Syntax, `src/Server/Domain.fs`, Ausgabe nach `reviews/` (B2).
- Rules: YAML gültig, keine lädt immer. Aber `domain-purity`, `persistence-safety`, `query-safety` zielen auf `src/Server/Domain.fs` (existiert nicht) bzw. `src/Server/Persistence.fs` und erfassen `src/Server/Ledger/`, `src/Server/Budgeting/` nicht [geprüft]. `build-verification`, `testing`, `feature-workflow` setzen Supabase, Smoke-Tests und `specs/` voraus. `localization.md` steht vollständig in einem HTML-Kommentar. `semantic-messaging`, `domain-purity` enthalten Beispiele aus einem anderen Projekt.
- Standards: `standards/` seit 2025-12 weitgehend unverändert; `domain-logic.md` zeigt `DateTime.UtcNow` als pure, `view-patterns.md` enthält DaisyUI, `api-tests.md` und `frontend-tests.md` passen nicht zur Praxis (B2, D). `standards/frontend/styleguide.md` ist aktuell und wird genutzt.

**Worker-Modell** [geprüft]: Kein Worker-Dispatch übergibt `model`; Worker laufen auf `claude-sonnet-5-5` (vorher `claude-sonnet-5`), Verifier auf `claude-opus-5-5`. Kein `CLAUDE_CODE_SUBAGENT_MODEL*` gesetzt.

**Gate**: Keine BC-README hat `## Test command` oder `## Runtime surface`; der Verifier ermittelt den Testbefehl selbst. Kein `.claude/settings.json` im Repo, keine Hooks, keine git-Hooks. Vorhandene Wächter: `token-guard`, `tailwind-class-guard`, `readme-delta-check`, `run-guards`, Expecto-Strukturtests (Cors, BackendPort, FontFace, TestDatabaseGuard).

**Settings**: Globaler `autoMode.environment` in `~/.claude/settings.json` beschreibt nur BudgetBuddy. Das Plugin `explanatory-output-style` fordert per SessionStart ausführliche Insights und widerspricht der globalen Kürzevorgabe.

**Auto-Memory**: `MEMORY.md` 7 Zeilen, 4,2 KB (die Indexzeile zu Tooling-Quirks ist sehr lang); Detaildatei `budgetbuddy-tooling-quirks.md` 48 KB, größtenteils Conductor-Lehren zu agentheim 0.9.2 bis 0.9.4, teils widersprüchlich. Umzugskandidaten laut A: YNAB-Eigenheiten in die ynab-sync-README, Deploy-Prüfweg, Screenshot-Rezept und Testbefehle in die infrastructure-README, Bot-Token-Hinweis nach `docs/bot-api.md`, Fable.Remoting-Testfalle in eine Testing-Rule.

**Legacy-Kandidaten** (A, Abschnitt 9): `.agentheim/next-step.md`, `.agentheim/salvage/`, `template/`, `legacy/`, `reviews/`, `standards/` außer `styleguide.md`, `docs/MILESTONE-PLAN.md`, `backlog.md`, die drei defekten Agents, die ungenutzten `fsharp-*`-Skills. `rules.yml` ist Produktdaten, kein Harness.

## 4. Tests (D)

- 2.463 Expecto-Fälle in 94 Dateien, 5 bis 6 s; rund 270 Node-Tests, davon etwa 28 Playwright-Wächter, rund 2 min.
- Tautologien: Stichprobe jede 40. Testkopfzeile (n=62), 2 Treffer (3,2 %); gezielte Suche fand rund 13 weitere, gehäuft in alten englischen Comdirect-/YNAB-Dateien.
- Mindestens 140 Quelltext-Strukturtests (5,7 %). Repo-weite Wächter sinnvoll; Teilstring-Prüfungen auf View-Verdrahtung und Klassenkonstanten schwach (auch B1 N7/N8, C B10/B19).
- Endpoints: von 75 Remoting-Endpoints 28 mit Positiv- und Negativtest, 13 ohne Test; Lücken in Settings, YNAB, Rules, Sync (Logik direkt in `Api.fs`). Bot vollständig abgedeckt. Kein Test über die echte Giraffe-Pipeline (deshalb konnte P1 durchrutschen).
- Stil: 1.650 deutsche, 424 englische Testnamen, 144 umschriebene Umlaute, `test`-CE und `testCase` gemischt.
- Doktrin-Vorschlag im Bericht D (acht Ebenen). Kern: Uhr und Zufall injizieren, pro Endpoint ein Adaptertest, ein Pipeline-Test über den echten Host, Strukturtests nur für repo-weite Invarianten, Live-Tests nur hinter Schalter.

## 5. Widersprüche zwischen Agents und offene Punkte

- P1: B2 (lokal reproduziert, 0 Byte) gegen Live-Messung (56 Byte). Offen.
- A korrigiert eine Annahme des Koordinators: Rules laden nicht wegen YAML-Fehlern immer; das Problem sind veraltete Pfade und Inhalte.
- D meldet, sein Bericht sei nach dem Schreiben von außen geändert worden (Dateistand 11:33). Ursache nicht geklärt; Inhalt nicht gegen D's Fassung abgeglichen.
- qa-milestone-reviewer und red-test-fixer haben laut A strikt geparst ebenfalls ungültiges YAML, laden aber (einzeilige description).
