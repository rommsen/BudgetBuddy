# Abgleich BudgetBuddy mit Souls (Phase 3)

Stand: 2026-10-29. Grundlage: `zusammenfassung.md` und die Berichte A bis D in diesem Ordner,
dazu `/Users/romansachse/src/souls/.agentheim/knowledge/research/harness-audit/zielbild.md` (erst nach
Abschluss der Erhebung gelesen) sowie die dortigen Berichte E und F.
Dieser Bericht trifft keine Entscheidungen. Die Liste in Abschnitt 4 ist nach der ersten Souls-Messung
abzuarbeiten (Souls-Zielbild M6).

## 1. Gleiche Befunde in beiden Projekten: Kandidaten für `~/.claude/`

| # | Befund | Souls | BudgetBuddy | Kandidat global |
|---|---|---|---|---|
| G1 | Worker laufen auf Sonnet, weil `agentheim/agents/worker.md:5` `model: sonnet` pinnt und der Conductor kein `model` übergibt | 74 Dispatches, 4 mit Opus | 77 Dispatches, 0 mit `model`; 93 Subagent-Transkripte auf Sonnet [geprüft] | Ja. `CLAUDE_CODE_SUBAGENT_MODEL` in `~/.claude/settings.json` `env` wirkt in allen agentheim-Projekten. Souls hat projektlokal entschieden (E1); global ist einfacher, aber wirkt dann auf jeden Subagent jedes Projekts. |
| G2 | Prosa-Pflichtregeln tragen wenig; die schweren Befunde fanden Reviews und Fachdoku, nicht Regeln | B/C: Regeln eingehalten, Fehler in Verhalten über mehrere Pfade | B1 markiert alle Produktbefunde „ohne Regelbezug“; Regeln greifen nur bei Schichtung und Styleguide | Grundsatz für den `learn`-Skill: Reihenfolge Test/Wächter vor Prosa. Global als Skill sinnvoll. |
| G3 | Fehler liegen außerhalb des einzelnen Diffs bzw. in fehlender Pipeline-/Endpoint-Abdeckung | Nicht-Member-Negativtests fehlen bei 46 von 66 Endpoints | 13 von 75 Remoting-Endpoints ohne Test, kein Test über die echte Giraffe-Pipeline (P1 möglich) | Projektlokal (Wächter sind projektspezifisch), aber das Muster „ein tabellengetriebener Test über alle Endpoints“ gehört in eine gemeinsame Test-Doktrin. |
| G4 | Lernmechanismus | Auto-Memory, 27 Dateien, 15 davon Conductor-Wissen für agentheim | Auto-Memory, 1 Detaildatei mit 48 KB, überwiegend Conductor-Wissen agentheim 0.9.2 bis 0.9.4, teils widersprüchlich | Ja. `learn`-Skill unter `~/.claude/skills/learn/`, `autoMemoryEnabled: false` global. Das Conductor-Wissen beider Projekte gehört in ein gemeinsames Upstream-Sammel-Issue an agentheim, nicht in zwei Projekt-Repos. |
| G5 | Serena: Pflicht in CLAUDE.md, praktisch keine Nutzung, Subagents ohne Zugriff, im Kontext `claude-code` fehlen `search_for_pattern`/`find_file`/`list_dir` | 0 Aufrufe seit 09-26 | Subagents 0, Hauptsession 41 im Oktober; 2.036 Bash-Zugriffe der Worker auf `.fs`; `search_for_pattern` schlug in dieser Session fehl [geprüft] | Teilweise global: `~/.serena/` und ein evtl. globaler MCP-Eintrag. Der Pflichtblock steht in der Projekt-CLAUDE.md. |
| G6 | Kein `## Test command` in BC-READMEs, keine Projekt-Hooks, keine git-Hooks | ja | ja (7 BCs) | Nein, projektlokal (Gate-Befehl unterscheidet sich). Global nur die Konvention „jede BC-README nennt den Gate-Befehl“ als Teil des `learn`/agentheim-Leitfadens. |
| G7 | Schreibstil-Regeln in `~/.claude/CLAUDE.md` | behalten | gilt ebenso, Berichte halten sie ein | Bleibt global. Umlaut- und Gedankenstrich-Grep als optionaler Check für Doku (A, Abschnitt 8). |
| G8 | git-Verbote als Prosa statt Permissions | Push-Verbot, `git add -A` | Commit-/Push-Regeln aus agentheim und CLAUDE.md; autoMode `soft_deny` für `git push deploy` | Ja für die allgemeinen: `permissions.ask` für `git push`, `deny` für `git add -A`/`commit -a` in `~/.claude/settings.json`. Projektspezifische Deploy-Remotes bleiben projektlokal. |
| G9 | Plugin `explanatory-output-style` widerspricht der Kürzevorgabe | Owner: bleibt | Insight-Blöcke in jeder Antwort dieser Session | Global entschieden (Souls). Nur zur Kenntnis. |
| G10 | Legacy aus der Vor-agentheim-Zeit: `fsharp-*`-Skills (13, je 0 Aufrufe), Milestone/Spec-Pfade, veraltete Rules mit Supabase/Smoke-Verweisen | ja | ja; BudgetBuddy-Rules verweisen sogar auf Supabase und `RUN_SMOKE_TESTS`, die es hier nie gab (aus Souls übernommen) | Die `fsharp-*`-Skills existieren in beiden Repos als Kopie. Entfallen sie in beiden, ist nichts zu globalisieren. Falls Restwissen bleibt: ein gemeinsamer Ort unter `~/.claude/skills/` statt zwei Kopien. |

## 2. Abweichungen und ihre Gründe

| # | Thema | Souls | BudgetBuddy | Grund (Befund) |
|---|---|---|---|---|
| A1 | Gate-Befehl | `./verify.sh` (Build, Fable, Supabase, Unit, E2E, Wächter) | Kein verify.sh; Expecto 5 bis 6 s, Node/Playwright-Wächter rund 2 min über `npm run test:scripts`, `readme-delta-check` | Andere Infrastruktur (SQLite statt Supabase). Der Gate-Befehl muss eigens definiert werden, etwa `dotnet test` plus `npm run test:scripts`. |
| A2 | Hauptrisiko | Privacy über Space-Grenzen, mehrere Nutzer | Secrets und Fremdzugriff: Bot-Token, `getSettings` liefert Secrets im Klartext, `/api` ohne Auth, Comdirect-Live-Login im Testlauf | Einzelnutzer-App hinter Tailscale/Caddy mit Bankzugang. Die Security-Pflichtliste der Test-Doktrin muss hier Secrets, Live-Dienste und Bot-Grenzen abdecken, nicht Space-Membership. |
| A3 | Review-Agents | review-backend/-frontend/-tests aktiv genutzt | Drei Review-Agents laden gar nicht (YAML), inhaltlich veraltet; `code-reviewer` 1 Aufruf | In BudgetBuddy gibt es keinen gelebten Review-Pfad außer dem Verifier. Kürzen wie in Souls reicht nicht, zuerst ist zu entscheiden, ob es sie braucht. |
| A4 | Rules-Pfade | Pfade passen weitgehend | `domain-purity`, `persistence-safety`, `query-safety` zielen auf nicht existierende bzw. nur einen Teil der Dateien; BC-Ordner `src/Server/Ledger/`, `src/Server/Budgeting/` ungedeckt | BudgetBuddy wurde nach den Rules in BC-Ordner umgebaut. Wer Rules behält, muss Pfade auf `src/Server/**/Domain.fs` usw. ändern oder sie durch Wächter ersetzen. |
| A5 | Diary | archivieren | wird befolgt (210 Commits), 434 KB; doppelt zu protocol.md, Task-Outcome, Commit-Message | Gleicher Befund, aber in BudgetBuddy aktiv gepflegt. Archivieren spart hier messbar Arbeit pro Task. |
| A6 | `backlog.md`, `docs/MILESTONE-PLAN.md`, `qa-milestone-reviewer` | Spec-Pfad | Seit 2026-06 bzw. 2025-12 nicht mehr genutzt, qa-milestone-reviewer 0 Dispatches, trotzdem rund 1,6k Tokens Pflichttext in CLAUDE.md | Gleiche Ursache (Vor-agentheim-Ablauf), anderes Artefakt. |
| A7 | CLAUDE.md-Inhalt | Domäne und Privacy | Design-System-API-Referenz (Z. 527 bis 909, etwa 2,5k Tokens), RemoteData-Beispiele, Code-Patterns | Die Design-System-Referenz ist aus dem Code ablesbar und hat mit `/styleguide` und `standards/frontend/styleguide.md` schon zwei Quellen. |
| A8 | Möglicher Produktfehler im Routing (P1) | kein Pendant | `next ctx` statt `skipPipeline` vor Fable.Remoting; Standards-Text dazu irreführend | Projektbefund; zeigt, dass ein falscher Standard schaden kann. Spricht für Eindampfen von `standards/` wie in Souls (E6). |
| A9 | Doppeltes Laden in Worktrees | Override (E) injiziert Rules zusätzlich | Ohne Injektion trotzdem doppelt: 28 von 78 Worker-Transkripten laden CLAUDE.md/Rules aus Hauptbaum und Worktree | Neuer Befund. Er gilt vermutlich auch in Souls, unabhängig von Override (E); dort im Rahmen von M11 prüfen. |

## 3. `~/.claude/settings.json`: was global bleibt, was projektlokal wird

Ist-Zustand (gelesen): `autoMode.environment` beschreibt ausschließlich BudgetBuddy (Repo
`rommsen/BudgetBuddy` öffentlich, `.env`-Secrets `BUDGETBUDDY_ENCRYPTION_KEY`, `YNAB_TOKEN`,
`COMDIRECT_*`, Deploy-Remote `docker-host:/opt/repos/budgetbuddy.git`, „Trusted repo: /Users/romansachse/src/BudgetBuddy“).
`autoMode.allow` enthält Homelab-Freigaben, `autoMode.soft_deny` die BudgetBuddy-Einträge
`git push deploy:*` und `scripts/deploy-rules.sh … --clear`. Laut A geht der Text an den
Auto-Mode-Klassifikator, nicht in den Modellkontext (Einschätzung). Folge: In Souls und Homelab urteilt
der Klassifikator mit BudgetBuddy als „trusted repo“.

Vorschlag:

| Bleibt global | Wird projektlokal (`.claude/settings.json` des jeweiligen Repos, eingecheckt oder `settings.local.json`) |
|---|---|
| Org-weite Zeilen ohne Projektbezug: keine Organisation, kein Cloud-Provider, „Host containment: normale Entwicklermaschine“, „Public paste services außerhalb der Vertrauensgrenze“ | BudgetBuddy: Repo-Sichtbarkeit, Remotes, Deploy-Remote, `.env`-Variablen, „Trusted repo“, `soft_deny` für `git push deploy` und `deploy-rules.sh --clear` |
| Allgemeine Sicherheitsgrenze: Produktionsdaten nicht lesen ohne Rückfrage (hat in dieser Session gegriffen) | Souls: eigene Beschreibung (Supabase lokal, Repo, Deploy-Weg) |
| `env` für Subagent-Modell, falls G1 global entschieden wird | Homelab-`allow`-Einträge in das Homelab-Repo, sofern dort eine Session läuft; sonst als globale Zeile mit ausdrücklichem Pfadbezug (heute schon so formuliert) |
| `permissions.ask` für `git push`, `deny` für `git add -A`/`commit -a` (G8) | |
| `autoMemoryEnabled: false` (G4) | |

Offen und vor Umsetzung in der Claude-Code-Doku zu prüfen: ob `autoMode`-Einträge in
Projekt-Settings überhaupt zusammengeführt werden oder nur global wirken. Wenn nur global: den
Abschnitt in projektbezogene Blöcke („In /Users/romansachse/src/BudgetBuddy: …“) gliedern, wie es die
Homelab-Zeile schon tut.

## 4. Entscheidungsliste für den Owner

Jede Zeile ist einzeln entscheidbar, zu treffen nach der ersten Souls-Messung. Die Empfehlung ist
meine; Begründung in Kurzform.

| # | Entscheidung | Optionen | Empfehlung |
|---|---|---|---|
| D0 | **Sofort, unabhängig von der Messung:** P1 (Routing, `/api` evtl. leer) am Live-System prüfen und ggf. als Bug-Task erfassen | prüfen / nicht prüfen | Prüfen. Wenn bestätigt, ist das wichtiger als jede Harness-Frage. Bei der Prüfung lesen, nicht schreiben. |
| D1 | Produktbefunde P2 bis P7 als Backlog-Tasks erfassen (ledger, infrastructure, ynab-sync) | alle / nur P3, P4, P6, P7 / keine | P3, P4, P6, P7 zuerst: sie haben einen konkreten Fehlerfall. P2 braucht vorher eine Abwägung zu ADR 0068 (Drossel pro Token-Status statt global). |
| D2 | Worker-Modell (G1) | global `env` / projektlokal wie Souls / so lassen | Wie Souls entscheiden, aber den Ort erst nach der Souls-Messung festlegen. Falls Opus-Worker in Souls besser abschneiden: global, damit beide Projekte gleich laufen. |
| D3 | Auto-Memory aus und Memory-Triage (G4) | global aus / nur hier aus | Global aus. Detaildatei nach Tabelle 4 in Bericht A verteilen; Conductor-Wissen ins gemeinsame Upstream-Sammel-Issue. |
| D4 | `learn`-Skill (G2, G4) | global unter `~/.claude/skills/` / je Projekt | Global, mit projektneutraler Ortsreihenfolge (Test/Wächter, Hook/Permission, Gate-Skript, BC-README, Rule, Skill, CLAUDE.md). |
| D5 | Gate-Befehl in BC-READMEs (G6, A1) | `dotnet test` / `dotnet test` + `npm run test:scripts` / neues `verify.sh` | Zuerst klären, was ein Worker-Lauf heute tatsächlich ausführt; dann einen Befehl festlegen und als `## Test command` in alle sieben BC-READMEs. Ein kleines Skript bündelt besser als zwei Befehle. |
| D6 | Pipeline- und Endpoint-Wächter (G3) | ja / nein | Ja: ein Test über den echten Giraffe-Host (deckt P1-Klasse), ein tabellengetriebener Test über alle Remoting-Endpoints mit Pflicht-Negativfall. |
| D7 | Live-Dienste im Testlauf (P6) | Schalter erzwingen / Test löschen | Schalter erzwingen und einen Wächter, der jeden Test mit Netzaufruf auf den Schalter prüft. |
| D8 | Serena (G5) | entfernen / behalten ohne Pflicht | Entfernen wie Souls; CLAUDE.md-Block Z. 1 bis 27 und `serena-tools.md` entfallen. |
| D9 | Projekt-CLAUDE.md eindampfen (A6, A7) | Ziel rund 2.500 Tokens wie Souls / nur Pflichtteile streichen | Nach Souls-Messung entscheiden. Unabhängig davon streichbar: Serena-Block, Milestone/Backlog/QA-Pflichten, Design-System-API-Referenz. |
| D10 | Diary (A5) | archivieren / behalten | Archivieren wie Souls; das spart einen Schritt pro Task. Bug-Fix-Protokoll (Regressionstest mit Kommentar) bleibt, es wird befolgt (B1). |
| D11 | Rules (A4) | Pfade korrigieren / durch Wächter ersetzen / löschen | Je Rule wie Souls 1.2. Zuerst `localization.md` (auskommentiert), `build-verification`, `feature-workflow`, `serena-tools` löschen. `domain-purity` als Wächter über `src/Server/**/Domain.fs`. |
| D12 | Review-Agents (A3) | reparieren und kürzen / löschen / einen neuen schreiben | Löschen der drei defekten Agents, `code-reviewer` behalten oder ebenfalls löschen. Kein gelebter Pfad, der Verifier deckt Diff-Reviews ab. Ein periodisches Bestandsreview wie Souls M18 ist der bessere Ersatz (C und B2 fanden die schweren Befunde ohne Regeln). |
| D13 | `qa-milestone-reviewer`, `red-test-fixer`, `fsharp-*`-Skills, `backlog.md`, `docs/MILESTONE-PLAN.md`, `reviews/`, `template/`, `legacy/`, `.agentheim/next-step.md`, `.agentheim/salvage/` (G10, A6) | löschen / archivieren | Archivieren in einem Schritt, vorher nicht herleitbares Wissen prüfen (wie Souls M9). |
| D14 | `standards/` (A8) | eindampfen / löschen außer styleguide.md | Eindampfen; `standards/backend/overview.md:22-23` (Giraffe Some/None) zuerst korrigieren, weil er zu P1 beigetragen haben kann. |
| D15 | `~/.claude/settings.json` autoMode (Abschnitt 3) | aufteilen / so lassen | Aufteilen; vorher Doku prüfen, ob Projekt-Settings autoMode zusammenführen. |
| D16 | Test-Doktrin (D) | Souls-Doktrin übernehmen / eigene | Gemeinsamer Kern (Heimat-Ebene, keine Tautologien, Live nur hinter Schalter, Strukturtests nur für repo-weite Invarianten), projekteigene Security-Pflichtliste (Secrets, Bot-Grenzen, Pipeline). Stilvereinheitlichung (Umlaute in Testnamen, `test` vs. `testCase`) nur bei Gelegenheit. |
| D17 | Referenz-Tasks für eine BudgetBuddy-Baseline (analog Souls E12) | ja / nein | Ja, falls BudgetBuddy nach Souls verschlankt wird. Vorschlag: ledger-ssmp3 (Backend/Security), design-system-Task zu 897b13c (UI), ein Bug-Task mit Regressionstest. |

## Offene Punkte aus der Erhebung

- P1 Widerspruch zwischen lokaler Reproduktion (B2) und Live-Messung (56 Byte).
- D meldet eine nachträgliche Änderung seines Berichts durch Dritte; nicht geklärt.
- Ob `autoMode` projektlokal wirkt (Abschnitt 3).
- Doppeltes Laden in Worktrees (A9) auch in Souls?
