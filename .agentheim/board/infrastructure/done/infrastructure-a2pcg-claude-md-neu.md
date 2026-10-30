---
id: infrastructure-a2pcg
title: "Projekt-CLAUDE.md neu schreiben, Ziel 100 bis 130 Zeilen"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed: 2026-10-30
depends_on: [infrastructure-jy5ca, infrastructure-cbr0r, infrastructure-gwvw1, infrastructure-kq13v]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
29 KB, etwa 7,3k Tokens always-on, plus doppeltes Laden in Worktrees; Serena-Block, Milestone-, Backlog-, QA- und Diary-Pflichten werden nicht gelebt; Design-System-API-Referenz ist aus Code und Styleguide ablesbar (Audit A). Souls Abschnitt 2.

## What
Inhalt: Domäne (Kassenbuch, YNAB, Comdirect, Bob), Security-Fallen (Bot-Grenzen, Secrets, Live-Dienste), Laufzeitfallen (Fable.Remoting-Pflichtfelder, Giraffe `skipPipeline`, CLIMutable), Dev-Kommandos, Gate, Ablauf, Lernen, README-Deltas. Ohne CRITICAL/MUST-Nachdruck.

## Acceptance criteria
- [x] CLAUDE.md hat höchstens 130 Zeilen.
- [x] Der Abschnitt README-Deltas bleibt so, dass `npm run test:scripts` (Kopplung Skriptpfad/Text) grün ist.

## Notes
Migrationsschritt M12. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt. Nach diesem Schritt messen.

## Outcome

`CLAUDE.md` ist neu geschrieben: 117 Zeilen (AC höchstens 130), vorher 677. Gliederung nach Souls-Zielbild Abschnitt 2, mit BudgetBuddy-Fakten ersetzt.

Inhalt:
- Domäne: Kassenbuch und Spiegel, "BB entscheidet, YNAB wird nachgezogen", Comdirect-Import mit Reference-Dedup und `rules.yml`, Budget, Lena fragen (`lena_flag_color`, ADR 0056), Bob unter `/bot/` mit `preview`/`commit` (ADR 0066), Verweis auf context-map und BC-READMEs.
- Security-Fallen: öffentliches Repo (per `gh repo view` geprüft: PUBLIC), Bob-Grenzen nach ADR 0068 (500 € je Buchung, 500 €/10 Commits je Berliner Tag, 31 Tage, 10 Anfragen/min, kein Flag), Secrets nie an den Client mit offenem Bug `infrastructure-s1r95`, Live-Dienste nur hinter `RUN_INTEGRATION_TESTS`, `USE_MEMORY_DB`.
- Laufzeitfallen: Fable.Remoting-Pflichtfelder brechen Playwright-Fixtures, Giraffe `skipPipeline` statt `next ctx` (2a5c229, `src/Tests/RemotingPipelineTests.fs`), `[<CLIMutable>]` und `OptionHandler<'T>`, Milliunits `int64` und `Encode.int`, Validierung an der API-Grenze (vm8zz Punkt 5), Performance-Hinweis für Listen (vm8zz Punkt 9). vm8zz Punkt 6 steckt im Dapper-Bullet.
- Architektur (eine Zeile pro Schicht, Rules, Test-Doktrin, Styleguide als UI-Gate plus Route `/styleguide`), Dev-Kommandos, Gate `./verify.sh`, Ablauf (agentheim, Worktree-Skripte, scoped Commits, git-guard, Push/`git push deploy`/`deploy-rules.sh --clear` nur auf Auftrag bzw. nach Rückfrage, laut gfxn4 für den Auto-Mode-Klassifikator hier), Override (C) wörtlich, Lernen mit `learn`-Auslöser, Sprache, README-Deltas wörtlich.

Weggelassen, je mit Grund:
- Serena-Block: Serena ist entfernt (M8).
- "Your Role", "Before Implementing", Documentation Map, "Implementing User Specifications": generisches Blueprint-Material, die Rules unter `.claude/rules/` laden pfadbezogen.
- Development Order mit `src/Server/Domain.fs`: die Datei gibt es nicht, die BCs haben eigene Ordner.
- Qualitätskriterien mit QA-Pflicht und Verification Checklist: nicht gelebt; das Gate ist `./verify.sh`, Reviews schlägt Override (C) vor.
- Key Principles, RemoteData-Helfer, Code Patterns, Anti-Patterns: aus Code und `.claude/rules/` ablesbar. Die nicht ablesbaren Teile (Validierung an der Grenze, reine Domäne, keine Tests gegen echte DB) stehen je in einer Zeile.
- Bug Fix Protocol: steht in `.claude/rules/test-doctrine.md` ("Bugfixes"), CLAUDE.md behält eine Zeile (Zielbild E11).
- Quick Commands Docker/Build und Deploy-Rules-Erklärung: auf die Verbote und die Dev-Kommandos reduziert, Details stehen in `scripts/deploy-rules.sh`.
- Diary-Pflicht: Diary ist eingefroren, eine Zeile sagt das.
- Tech Stack Tabelle: in der Architektur-Zeile zusammengefasst.
- Design-System-API-Referenz (Button bis PageHeader): aus `src/Client/DesignSystem/` und `standards/frontend/styleguide.md` ablesbar.

Geprüft per grep/ls: alle genannten Pfade, Skripte, npm-Skripte, `Api.webApp`, `skipPipeline`, `OptionHandler`, `Encode.int`, `BOT_MAX_AMOUNT_EUR`, `RUN_INTEGRATION_TESTS`, `USE_MEMORY_DB`, `DATA_DIR`-Default, Port 5081, Remote `deploy`, Commit 2a5c229, `infrastructure-s1r95`, die vier Review-Agents. Keine Gedankenstriche im Text.

Nicht prüfbar: der globale `learn`-Skill existiert noch nicht (`~/.claude/skills/` enthält ihn nicht), die Zeile nennt ihn als Auslöser wie im Souls-Entwurf. `/architecture-review` liegt nicht im Repo (`.claude/commands/` hat nur `blogpost.md`); der Name steht unverändert aus Override (C). Dass ein neues Pflichtfeld auch ältere Clients bricht, stammt aus Dispatch und Memory, nicht aus einem Test.

Gate: `./verify.sh` endet mit `VERIFY: PASS` (dotnet 2605 grün, 8 opt-in übersprungen; node 319 grün, darunter T8 und der Harness-Check). Keine andere Datei geändert, kein Diary-Eintrag.

### Nachträge des Conductors
- Iteration 2: Verifier-FAIL zu Zeile 47 ("Beträge sind YNAB-Milliunits als `int64`"). Korrigiert auf `Money` (decimal) im Modell und Milliunits mit Schreibweg `int` an der YNAB-Grenze. Kopplungstests danach 28 von 28 grün.
- Iteration 3: `/agentheim:model` auf den echten Skillnamen `/agentheim:modeling` korrigiert, beim Durchlesen mit dem Owner gefunden.
- Abnahme durch den Owner am 2026-10-30 ("passt").
- Folge: `standards/frontend/styleguide.md` verweist an drei Stellen auf den entfallenen Abschnitt "Design System Components", erfasst als Backlog-Item.
