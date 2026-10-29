# Zielbild BudgetBuddy-Harness

Stand: 2026-10-29. Auftrag: Handoff v2 (Souls-Commit e14f88d). Die Entscheidungen aus
`/Users/romansachse/src/souls/.agentheim/knowledge/research/harness-audit/zielbild.md` (E1 bis E12) gelten
als Default. Grundlage für BudgetBuddy: `a-inventar-nutzung.md`, `b1-…`, `b2-…`, `c-review-ohne-harness.md`,
`d-test-qualitaet.md`, `zusammenfassung.md`, `abgleich-souls.md` in diesem Ordner. Phase-1-Agent C (neue
Code-Stichprobe) entfiel, weil B1, B2 und C vom Vormittag acht Commits schon abdecken (Owner-Entscheidung).

## 1. Übernahmetabelle

| E | Souls-Entscheidung | BudgetBuddy | Begründung |
|---|---|---|---|
| E1 | Worker auf Opus | übernommen | 77 Worker-Dispatches ohne `model`, Worker liefen auf `claude-sonnet-5-5` [geprüft]. Gleicher env-Block in `.claude/settings.json` (projektlokal, siehe G). |
| E2 | Serena entfernen | übernommen | Subagents 0 Aufrufe, 2.036 Bash-Zugriffe auf `.fs`; Hauptsession 41 Aufrufe im Oktober, durch die Pflicht in CLAUDE.md:1-27 erzwungen. `search_for_pattern` existiert im Kontext `claude-code` nicht [geprüft]. |
| E3 | Gate deterministisch, `## Test command` in BC-READMEs, Wächter, git-guard-Hook, kein Stop-Hook | übernommen mit Anpassung | Es gibt kein `verify.sh`. Verifier bekamen 38-mal nur `dotnet test`, 17-mal die volle Kette mit `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts` (Transkripte). `verify.sh` entsteht neu und bündelt diese Kette; Lock nur für `test:scripts`, falls nötig (Expecto läuft in-memory). Namen roter Tests ausgeben. |
| E4 | Verifier Pflicht; review-code löschen; Review-Agents kürzen, Session-End-Empfehlung | übernommen mit Anpassung | `review-code.md` (`code-reviewer`) löschen. `backend-code-quality`, `frontend-architecture`, `design-quality` laden heute gar nicht (YAML) und sind veraltet (DaisyUI, Tailwind 3, `src/Server/Domain.fs`). Reparieren und kürzen statt löschen: ihr nachgespielter Lauf (B2) fand den `/api`-Ausfall (Fix 2a5c229). `qa-milestone-reviewer` (0 Dispatches) wird zu einem gekürzten Test-Reviewer gegen die Test-Doktrin. |
| E5 | Legacy löschen | übernommen | `fsharp-*` (12) und `tailscale-deploy` 0 Aufrufe; `backlog.md` (zuletzt 2026-06-11), `docs/MILESTONE-PLAN.md` (2025-12-06), `reviews/`, `template/`, `legacy/`, `.agentheim/next-step.md`, `.agentheim/salvage/`, `red-test-fixer`. Nicht herleitbares Wissen vorher retten, Bezeichner per grep prüfen. `web-animation-design`: löschen, kein Nutzer im Repo. |
| E6 | `standards/` eindampfen | übernommen mit Anpassung | `standards/frontend/styleguide.md` bleibt vollständig (aktiv, in CLAUDE.md als Gate genannt, `/styleguide`-Route). Zuerst `standards/backend/overview.md:22-23` (Giraffe Some/None) korrigieren, der Text passte zum `/api`-Ausfall. |
| E7 | Test-Doktrin, Security-Pflichtliste, keine Property-Pflicht | übernommen mit Anpassung | Security-Pflichtliste BudgetBuddy-spezifisch: Pipeline-Test über den echten Host, Negativtest je Remoting-Endpoint, Live-Dienste nur hinter `RUN_INTEGRATION_TESTS`, Bot-Grenzen, keine Secrets an den Client. FsCheck bleibt erlaubt (10 Dateien nutzen es). ApiSmokeTests gibt es nicht. |
| E8 | `learn`-Skill plus Auslöser in CLAUDE.md | übernommen mit Anpassung | Global unter `~/.claude/skills/learn/` (Owner, Frage „Global“). Ortsreihenfolge mit `verify.sh` statt Souls-Spezifika; `diary/lessons.md` gibt es hier nicht. |
| E9 | diary/ archivieren, blogpost bleibt | **abweichend, Owner-Entscheidung** | Siehe Abschnitt 2. |
| E10 | Overrides: nur (C) bleibt | übernommen mit Anpassung | BudgetBuddy hat keine Overrides (A) bis (F). (C) wird neu eingeführt (Session-End-Empfehlung der Review-Agents, siehe E4). Der Abschnitt „README-Deltas (Agentheim)“ bleibt, er ist mechanisch an `scripts/readme-delta-check.mjs` gekoppelt (`npm run test:scripts`). |
| E11 | `/bug-fix` löschen, Bug-Szenarien in den Refine | übernommen mit Anpassung | Kein `/bug-fix`-Skill vorhanden. Das „Bug Fix Protocol“ in CLAUDE.md wird befolgt (B1, Fix 2a5c229 mit Regressionstest und Kommentar). Inhalt wandert in Test-Doktrin und BC-README-Hinweis (kaputt-/intakt-Szenario), CLAUDE.md behält eine Zeile. |
| E12 | Baseline-Replay mit Referenz-Tasks | übernommen mit Anpassung | Referenz-Tasks: `ledger-ssmp3` (Backend, Security, cfb05ff), `design-system-j6dnj` (UI, 897b13c), `ledger-ybm5x` (Bug, a0a8cab). Skript nach Vorlage `souls/scripts/baseline-replay.sh`. |
| fest | Auto-Memory aus | übernommen | `autoMemoryEnabled: false` erst nach der Triage (M7). |

## 2. Abweichungen mit Owner-Entscheidung

**E9 Diary (2026-10-29):** `diary/` ist ein Jekyll-Blog, `.github/workflows/deploy-diary.yml` deployt bei
jeder Änderung unter `diary/**`; `blogpost` schreibt nach `diary/_posts/`. Entscheidung:
- `diary/development.md` wird eingefroren, die Diary-Pflicht in CLAUDE.md entfällt.
- `blogpost` bleibt als Möglichkeit und liest künftig `.agentheim/board/protocol.md` und git log.
- Nichts Externes mehr: der Pages-Deploy (`deploy-diary.yml`) wird abgeschaltet; Posts bleiben lokal.

**Globale Ebene (2026-10-29), „teilweise global“:**
- Global: `learn` nach `~/.claude/skills/learn/` (danach die Souls-Kopie entfernen oder auf die globale verweisen; mit der Souls-Session abstimmen). `autoMode` in `~/.claude/settings.json` gliedern: projektneutrale Org-Zeilen plus Blöcke je Projekt mit Pfadbezug (vorher in der Doku prüfen, ob projektlokale `autoMode`-Einträge zusammengeführt werden).
- Projektlokal: `git-guard.sh` als PreToolUse-Hook und der Opus-env-Block, jeweils in `.claude/settings.json` des Repos.
- Schreibstil bleibt in `~/.claude/CLAUDE.md`.

## 3. Migrationsplan

Vorlage: Souls M0 bis M18. Gestrichen: M15 (Souls-Gesten-Slice), M16 (LSP-Spike, Ergebnis aus Souls übertragbar),
M17 (Szenario-Testsprache, erst nach dem Souls-Spike übernehmen). „Conductor“ heißt: Task bleibt im Backlog,
der Owner begleitet sie, kein Worker.

| # | Schritt | BC | Art | Wer |
|---|---|---|---|---|
| M0 | `.claude/settings.json` mit Opus-env; `scripts/baseline-replay.sh` nach Souls-Vorlage; Baseline der drei Referenz-Tasks mit heutigem Harness | infrastructure | chore | Worker, Baseline Conductor |
| M1 | Produktbefunde: P3 Betragsdeckel fällt bei Unsinn auf das Maximum; P4 `entry_id: null` nach gescheitertem Push (Doppelbuchung); P5 hängender Commit-Task; P2 Drossel vor Auth (Abwägung zu ADR 0068 im Refine) | ledger | bug | Worker |
| M2 | Produktbefunde: P6 Comdirect-/YNAB-Tests ohne `RUN_INTEGRATION_TESTS`-Prüfung; P7 `getSettings` liefert Secrets an den Client | banking-import, ynab-sync, infrastructure | bug | Worker |
| M3 | `verify.sh` (Build, Tests, Client-Build, Guards, `test:scripts`, Namen roter Tests, ggf. Lock); `## Test command` in alle sieben BC-READMEs; `git-guard.sh` plus Test nach Souls-Vorlage als PreToolUse-Hook | infrastructure | chore | Worker; README-Deltas über Conductor |
| M4 | Worktree-Skripte (node_modules-Symlink anlegen und vor `worktree remove` löschen) | infrastructure | chore | Worker |
| M5 | Wächter Security: Pipeline-Test über `Api.webApp` mit echten Remoting-Handlern; tabellengetriebener Endpoint-Test (Reflection über die Remoting-Interfaces, neuer Endpoint ohne Zeile macht rot, Pflicht-Negativfall); Wächter „Netzaufruf nur hinter Schalter“ | infrastructure | feature | Worker |
| M6 | Wächter Struktur: `[<CLIMutable>]` auf Dapper-Row-Typen (Vorlage `CliMutableGuard.Tests.fs`), Domain pur über `src/Server/**/Domain.fs`, SQL ohne Interpolation über `**/Persistence.fs` und `QueryHandlers.fs` | infrastructure | feature | Worker, danach Messen |
| M7 | `learn` global anlegen (Abstimmung mit Souls); Memory-Triage (Detaildatei 48 KB nach Tabelle 4 in Bericht A); Conductor-Wissen in das gemeinsame Upstream-Sammel-Issue; danach `autoMemoryEnabled: false`, Memory-Ordner archivieren | infrastructure | chore | Conductor |
| M8 | Serena entfernen: MCP-Eintrag in `~/.claude.json` (Owner), `.serena/`, Freigaben in `.claude/settings.local.json`, `.claude/rules/serena-tools.md`, CLAUDE.md:1-27 | infrastructure | chore | Conductor |
| M9 | Legacy löschen nach E5; vorher Wissen aus `fsharp-*`, `tailscale-deploy`, `standards/` retten und gegen den Code prüfen | infrastructure | chore | Worker |
| M10 | Rules konsolidieren: löschen `build-verification`, `feature-workflow`, `serena-tools`, `localization` (auskommentiert); `domain-purity`, `persistence-safety`, `query-safety` durch M6-Wächter ersetzen, Resttext mit Pfaden `src/Server/**`; `testing.md` durch `test-doctrine.md`; `semantic-messaging` auf BudgetBuddy-Beispiele; `standards/` eindampfen (E6); drei Review-Agents reparieren und kürzen, `qa-milestone-reviewer` zum Test-Reviewer, `code-reviewer` und `red-test-fixer` löschen | infrastructure | refactor | Worker, danach Messen |
| M11 | Override (C) einführen; BC-README-Übergaben (Szenario-Regel für Bugs, Hinweis Test-Doktrin) | infrastructure | chore | Conductor |
| M12 | Neue CLAUDE.md, Ziel 100 bis 130 Zeilen: Domäne (Kassenbuch, YNAB, Comdirect, Bob), Security-Fallen (Bot-Grenzen, Secrets, Live-Dienste), Laufzeitfallen (Fable.Remoting-Pflichtfelder, Giraffe `skipPipeline`, CLIMutable), Dev, Gate, Ablauf, Lernen, README-Deltas; ohne Serena-Block, Milestone/Backlog/QA-Pflichten, Diary-Pflicht, Design-System-API-Referenz | infrastructure | chore | Conductor, danach Messen |
| M13 | Diary nach Abschnitt 2: `development.md` einfrieren, `deploy-diary.yml` abschalten, `blogpost` auf protocol.md und git log | infrastructure | chore | Worker |
| M14 | Tests aufräumen: Tautologien in alten Comdirect-/YNAB-Dateien, schwache Quelltext-Tests (View-Verdrahtung, Klassenkonstanten), Tagesgrenzen-Tests mit geteilten Kalendertagen | infrastructure | refactor | Worker |
| M15' | Geschäftsregeln aus `Validation.fs` in Ledger-Domain (Tagesgrenze, Kategorie-Auflösung, Zeitzone) | ledger | refactor | Worker |
| M18 | Bestandsreview-Command nach dem umgebauten Souls-`/architecture-review`; erster Lauf je BC nach M10 | infrastructure | chore | Worker, Lauf Conductor |
| G | `~/.claude/settings.json` `autoMode` gliedern | (global) | chore | Conductor, Owner |

Reihenfolge: M0 (Baseline) vor allem, was den Harness ändert. M1 und M2 unabhängig davon. Abbruchkriterium
wie Souls: verschlechtert sich ein Referenz-Task nach M10 oder M12, Schritt zurücknehmen und analysieren.

## 4. Lehren aus Souls für den work-Lauf

Aus dem Handoff v2 übernommen: Rückgabeformat inline im Worker-Prompt; Prämissen vor jeder Welle gegen den
Code prüfen; Testkommando mit Sperre früh bauen und rote Testnamen ausgeben; Worktree-Teardown Symlinks zuerst,
keine `&&`-Ketten; `**Result source:**` ergänzt der Conductor; gestagte Löschungen nach `merge --squash` nicht
erneut an `git add`; Verifier mit Opus, Lauf im Vordergrund, SCOPE-NOTE auf den eigenen Diff; gerettete
Beispiele per grep gegen den Code prüfen.
